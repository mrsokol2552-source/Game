/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewBackgroundRaster.cs
@module: presentation.pathfinding.worldgen.farview_background_raster
@purpose: Rasterizes cached background payload into far-view chunk textures so chunked far-view can render background without depending on the live background tilemap renderer, while background-specific RT composition stays separate from generic tile snapshot raster logic.
@entry: PENV-28, ProceduralEnvironment.CompositeChunkBackgroundPayload, ProceduralEnvironment.TryComposeChunkBackgroundPayloadRenderTexture
@api: partial class implementation for ProceduralEnvironment
@deps: far-view chunk metadata, background render payload, readable sprite textures, background grid transforms, runtime composite material
@data: cached per-sprite background samples and per-chunk background payload slices
@perf: medium; raster work happens during far-view bake and is limited to chunk textures while final composition can stay on GPU
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and far-view bake smoke tests
@config: far-view chunked bake and background inclusion settings in ProceduralEnvironment
@assets: background tile sprites, runtime-converted tile textures, and hidden far-view composite shader
@notes: this layer keeps far-view background rendering payload-driven while using a material-backed composite path when the shader is available
*/

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-FARVIEWBACKGROUNDRASTER]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewBackgroundRaster.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private const string FarViewBackgroundCompositeShaderName = "Hidden/ProceduralEnvironment/FarViewChunkComposite";
        private static readonly int FarViewBackgroundTextureId = Shader.PropertyToID("_BackgroundTex");

        private bool ShouldRasterizeFarViewBackground(FarViewChunk chunk)
        {
            var renderSource = chunk != null ? chunk.RenderSource : null;
            return UseFarViewChunkedBake
                && FarViewIncludeBackground
                && chunk != null
                && renderSource != null
                && renderSource.HasBackgroundPayload
                && renderSource.BackgroundPayload != null
                && renderSource.BackgroundPayload.Length > 0
                && _background != null
                && _backgroundGrid != null
                && chunk.CaptureBounds.size.x > 0f
                && chunk.CaptureBounds.size.y > 0f;
        }

        private void ReleaseFarViewBackgroundCompositeMaterial()
        {
            if (_farViewBackgroundCompositeMaterial == null)
                return;

            if (UnityEngine.Application.isPlaying)
                Destroy(_farViewBackgroundCompositeMaterial);
            else
                DestroyImmediate(_farViewBackgroundCompositeMaterial);
            _farViewBackgroundCompositeMaterial = null;
        }

        private bool TryGetFarViewBackgroundCompositeMaterial(out Material material)
        {
            material = _farViewBackgroundCompositeMaterial;
            if (material != null)
                return true;

            var shader = Shader.Find(FarViewBackgroundCompositeShaderName);
            if (shader == null)
                return false;

            _farViewBackgroundCompositeMaterial = new Material(shader)
            {
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
            };
            material = _farViewBackgroundCompositeMaterial;
            return material != null;
        }

        private string ResolveFarViewBackgroundBackendDebugLabel()
        {
            if (!UseFarViewChunkedBake || !FarViewIncludeBackground)
                return "disabled";
            if (_farViewBackgroundCompositeMaterial != null)
                return "shader-compose";
            return Shader.Find(FarViewBackgroundCompositeShaderName) != null ? "shader-compose" : "cpu-fallback";
        }

        private void EnsureChunkBackgroundTexture(FarViewChunk chunk, int width, int height)
        {
            if (chunk == null)
                return;
            if (chunk.BackgroundTexture != null
                && chunk.BackgroundTexture.width == width
                && chunk.BackgroundTexture.height == height)
            {
                return;
            }

            if (chunk.BackgroundTexture != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(chunk.BackgroundTexture);
                else
                    DestroyImmediate(chunk.BackgroundTexture);
            }

            chunk.BackgroundTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            chunk.BackgroundRasterizedPayloadVersion = 0;
            chunk.BackgroundRasterizedWidth = 0;
            chunk.BackgroundRasterizedHeight = 0;
            chunk.BackgroundRasterizedCellRect = default;
            chunk.BackgroundRasterizedCaptureBounds = default;
        }

        private static bool AreFarViewBoundsApproximatelyEqual(Bounds a, Bounds b)
        {
            const float epsilon = 0.0001f;
            return Mathf.Abs(a.center.x - b.center.x) <= epsilon
                && Mathf.Abs(a.center.y - b.center.y) <= epsilon
                && Mathf.Abs(a.center.z - b.center.z) <= epsilon
                && Mathf.Abs(a.size.x - b.size.x) <= epsilon
                && Mathf.Abs(a.size.y - b.size.y) <= epsilon
                && Mathf.Abs(a.size.z - b.size.z) <= epsilon;
        }

        private bool CanReuseChunkBackgroundTexture(FarViewChunk chunk, int width, int height)
        {
            var renderSource = chunk != null ? chunk.RenderSource : null;
            return chunk != null
                && renderSource != null
                && chunk.BackgroundTexture != null
                && renderSource.BackgroundPayloadVersion != 0
                && chunk.BackgroundRasterizedPayloadVersion == renderSource.BackgroundPayloadVersion
                && chunk.BackgroundRasterizedWidth == width
                && chunk.BackgroundRasterizedHeight == height
                && chunk.BackgroundRasterizedCellRect.Equals(renderSource.BackgroundCellRect)
                && AreFarViewBoundsApproximatelyEqual(chunk.BackgroundRasterizedCaptureBounds, chunk.CaptureBounds);
        }

        private bool TryResolveFarViewBackgroundSpriteSample(
            TileBase tile,
            out FarViewSpriteSample sample,
            out Color32 tint)
        {
            return TryResolveFarViewSpriteSample(tile, out sample, out tint);
        }

        private static Matrix4x4 ResolveFarViewBackgroundTransform(BackgroundRenderCellDecision decision)
        {
            var transform = decision.Transform;
            if (transform == default || transform == Matrix4x4.identity)
                transform = GetTileTransform(decision.Tile);
            return transform;
        }

        private void RasterizeFarViewBackgroundCell(
            Color32[] pixels,
            int width,
            int height,
            Vector3 captureMin,
            float invWidth,
            float invHeight,
            BackgroundRenderCellDecision decision,
            bool placeUnderExisting,
            ref bool changed)
        {
            if (!TryResolveFarViewBackgroundSpriteSample(decision.Tile, out var sample, out var tint))
                return;

            Vector3 worldMin = _background.CellToWorld(new Vector3Int(decision.Col, decision.Row, 0));
            Vector3 worldMax = _background.CellToWorld(new Vector3Int(decision.Col + 1, decision.Row + 1, 0));
            int xMin = Mathf.Clamp(Mathf.FloorToInt((worldMin.x - captureMin.x) * invWidth * width), 0, width - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt((worldMin.y - captureMin.y) * invHeight * height), 0, height - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt((worldMax.x - captureMin.x) * invWidth * width), xMin + 1, width);
            int yMax = Mathf.Clamp(Mathf.CeilToInt((worldMax.y - captureMin.y) * invHeight * height), yMin + 1, height);
            if (xMax <= xMin || yMax <= yMin)
                return;

            int cellWidth = xMax - xMin;
            int cellHeight = yMax - yMin;
            if (cellWidth <= 0 || cellHeight <= 0)
                return;

            var inverseTransform = ResolveFarViewBackgroundTransform(decision).inverse;
            for (int y = yMin; y < yMax; y++)
            {
                float localY = ((y - yMin) + 0.5f) / cellHeight;
                int row = y * width;
                for (int x = xMin; x < xMax; x++)
                {
                    float localX = ((x - xMin) + 0.5f) / cellWidth;
                    Vector3 samplePoint = inverseTransform.MultiplyPoint3x4(new Vector3(localX - 0.5f, localY - 0.5f, 0f));
                    float sampleU = samplePoint.x + 0.5f;
                    float sampleV = samplePoint.y + 0.5f;
                    if (sampleU < 0f || sampleU > 1f || sampleV < 0f || sampleV > 1f)
                        continue;

                    int sampleX = Mathf.Clamp(Mathf.FloorToInt(sampleU * sample.Width), 0, sample.Width - 1);
                    int sampleY = Mathf.Clamp(Mathf.FloorToInt(sampleV * sample.Height), 0, sample.Height - 1);
                    var backgroundColor = sample.Pixels[(sampleY * sample.Width) + sampleX];
                    if (backgroundColor.a == 0)
                        continue;

                    backgroundColor = ApplyFarViewTint(backgroundColor, tint);
                    if (backgroundColor.a == 0)
                        continue;

                    int pixelIndex = row + x;
                    var composite = placeUnderExisting
                        ? CompositeFarViewLayers(backgroundColor, pixels[pixelIndex])
                        : CompositeFarViewLayers(pixels[pixelIndex], backgroundColor);
                    if (!composite.Equals(pixels[pixelIndex]))
                    {
                        pixels[pixelIndex] = composite;
                        changed = true;
                    }
                }
            }
        }

        private bool CanComposeFarViewBackgroundOnRenderTexture(FarViewChunk chunk)
        {
            return chunk != null
                && ShouldRasterizeFarViewBackground(chunk)
                && !HasFarViewTilePayload(chunk);
        }

        private bool TryBuildChunkBackgroundPayloadTexture(FarViewChunk chunk, int width, int height)
        {
            if (!ShouldRasterizeFarViewBackground(chunk))
                return false;
            var renderSource = chunk.RenderSource;

            if (CanReuseChunkBackgroundTexture(chunk, width, height))
            {
                _farViewBackgroundChunkCacheHits++;
                return true;
            }

            _farViewBackgroundChunkCacheMisses++;

            EnsureChunkBackgroundTexture(chunk, width, height);
            if (chunk.BackgroundTexture == null)
                return false;

            var pixels = new Color32[width * height];
            bool changed = false;
            float invWidth = 1f / Mathf.Max(0.0001f, chunk.CaptureBounds.size.x);
            float invHeight = 1f / Mathf.Max(0.0001f, chunk.CaptureBounds.size.y);
            Vector3 captureMin = chunk.CaptureBounds.min;

            for (int i = 0; i < renderSource.BackgroundPayload.Length; i++)
            {
                RasterizeFarViewBackgroundCell(
                    pixels,
                    width,
                    height,
                    captureMin,
                    invWidth,
                    invHeight,
                    renderSource.BackgroundPayload[i],
                    placeUnderExisting: false,
                    ref changed);
            }

            if (!changed)
            {
                chunk.BackgroundRasterizedPayloadVersion = 0;
                return false;
            }

            chunk.BackgroundTexture.SetPixels32(pixels);
            chunk.BackgroundTexture.Apply(false, false);
            chunk.BackgroundRasterizedPayloadVersion = renderSource.BackgroundPayloadVersion;
            chunk.BackgroundRasterizedWidth = width;
            chunk.BackgroundRasterizedHeight = height;
            chunk.BackgroundRasterizedCellRect = renderSource.BackgroundCellRect;
            chunk.BackgroundRasterizedCaptureBounds = chunk.CaptureBounds;
            return true;
        }

        private bool TryComposeChunkBackgroundPayloadRenderTexture(FarViewChunk chunk, ref RenderTexture rt, int width, int height)
        {
            if (rt == null)
                return false;
            if (!CanComposeFarViewBackgroundOnRenderTexture(chunk))
                return false;
            if (!TryGetFarViewBackgroundCompositeMaterial(out var material) || material == null)
                return false;
            if (!TryBuildChunkBackgroundPayloadTexture(chunk, width, height))
                return false;

            var composedRt = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            composedRt.filterMode = FilterMode.Point;
            composedRt.wrapMode = TextureWrapMode.Clamp;
            material.SetTexture(FarViewBackgroundTextureId, chunk.BackgroundTexture);
            Graphics.Blit(rt, composedRt, material);
            RenderTexture.ReleaseTemporary(rt);
            rt = composedRt;
            return true;
        }

        private bool TryUpdateChunkFromBackgroundPayload(FarViewChunk chunk, int width, int height, float ppu)
        {
            if (!TryBuildChunkBackgroundPayloadTexture(chunk, width, height))
                return false;

            EnsureChunkTexture(chunk, width, height);
            if (chunk.Texture == null || chunk.BackgroundTexture == null)
                return false;

            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) != 0)
            {
                Graphics.CopyTexture(chunk.BackgroundTexture, chunk.Texture);
            }
            else
            {
                chunk.Texture.SetPixels32(chunk.BackgroundTexture.GetPixels32());
                chunk.Texture.Apply();
            }

            EnsureChunkSprite(chunk, width, height, ppu);
            return true;
        }

        private void CompositeChunkBackgroundPayload(FarViewChunk chunk, int width, int height)
        {
            if (chunk == null || chunk.Texture == null)
                return;
            CompositeChunkPayloadSources(chunk, width, height);
        }
    }
}
