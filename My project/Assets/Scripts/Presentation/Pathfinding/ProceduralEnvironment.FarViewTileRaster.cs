/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewTileRaster.cs
@module: presentation.pathfinding.worldgen.farview_tile_raster
@purpose: Holds sprite-sampled tile snapshot raster helpers so far-view chunk payload rendering can consume extracted tile data without keeping generic tile logic mixed into the background backend.
@entry: PENV-30, ProceduralEnvironment.TryUpdateChunkFromPayloadSources
@api: partial class implementation for ProceduralEnvironment
@deps: far-view chunk render source, readable sprite textures, tilemap chunk snapshots
@data: cached sprite samples and tile snapshot raster helpers used by payload-only and mixed far-view chunk paths
@perf: medium; tile snapshot raster is hot during chunk bake and should stay reusable across backends
@thread: main thread only
@tests: indirect coverage via Unity recompilation, far-view HUD counters, and repo audits
@config: far-view chunked bake and tilemap inclusion settings in ProceduralEnvironment
@assets: tile sprites and runtime-readable textures
@notes: keep generic tile snapshot raster helpers separate from background-only composition so future GPU backends can replace this layer independently
*/

using System;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-FARVIEWTILERASTER]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewTileRaster.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private sealed class FarViewSpriteSample
        {
            public Color32[] Pixels;
            public int Width;
            public int Height;
        }

        private FarViewSpriteSample BuildFarViewSpriteSample(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return null;

            var readable = GetReadableTexture(sprite.texture);
            if (readable == null)
                return null;

            Rect rect = sprite.textureRect;
            int cropX = Mathf.RoundToInt(rect.x);
            int cropY = Mathf.RoundToInt(rect.y);
            int cropW = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            int cropH = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            var srcPixels = readable.GetPixels(cropX, cropY, cropW, cropH);
            if (srcPixels == null || srcPixels.Length == 0)
                return null;

            if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)
                srcPixels = UnrotatePackedPixels(srcPixels, cropW, cropH, sprite.packingRotation, out cropW, out cropH);

            int fullW = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.width));
            int fullH = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.height));
            int offX = Mathf.RoundToInt(sprite.textureRectOffset.x);
            int offY = Mathf.RoundToInt(sprite.textureRectOffset.y);
            if (fullW != cropW || fullH != cropH || offX != 0 || offY != 0)
            {
                var fullPixels = new Color[fullW * fullH];
                int srcStartX = 0;
                int srcStartY = 0;
                int dstStartX = offX;
                int dstStartY = offY;
                if (dstStartX < 0)
                {
                    srcStartX = -dstStartX;
                    dstStartX = 0;
                }
                if (dstStartY < 0)
                {
                    srcStartY = -dstStartY;
                    dstStartY = 0;
                }

                int copyW = Mathf.Min(cropW - srcStartX, fullW - dstStartX);
                int copyH = Mathf.Min(cropH - srcStartY, fullH - dstStartY);
                if (copyW > 0 && copyH > 0)
                {
                    for (int y = 0; y < copyH; y++)
                    {
                        int srcRow = (srcStartY + y) * cropW;
                        int dstRow = (dstStartY + y) * fullW;
                        Array.Copy(srcPixels, srcRow + srcStartX, fullPixels, dstRow + dstStartX, copyW);
                    }
                }

                srcPixels = fullPixels;
                cropW = fullW;
                cropH = fullH;
            }

            var samplePixels = new Color32[srcPixels.Length];
            for (int i = 0; i < srcPixels.Length; i++)
                samplePixels[i] = srcPixels[i];

            return new FarViewSpriteSample
            {
                Pixels = samplePixels,
                Width = cropW,
                Height = cropH
            };
        }

        private bool TryResolveFarViewSpriteSample(
            TileBase tile,
            out FarViewSpriteSample sample,
            out Color32 tint)
        {
            sample = null;
            tint = new Color32(255, 255, 255, 255);
            if (tile == null)
                return false;

            var sprite = ExtractTileSprite(tile);
            if (sprite == null)
                return false;

            if (!_farViewBackgroundSpriteCache.TryGetValue(sprite, out sample) || sample == null)
            {
                sample = BuildFarViewSpriteSample(sprite);
                _farViewBackgroundSpriteCache[sprite] = sample;
            }

            if (sample == null
                || sample.Pixels == null
                || sample.Pixels.Length == 0
                || sample.Width <= 0
                || sample.Height <= 0)
            {
                return false;
            }

            tint = ResolveFarViewTileTint(tile);
            return true;
        }

        private static Color32 ResolveFarViewTileTint(TileBase tile)
        {
            if (tile is Tile unityTile)
                return (Color32)unityTile.color;
            return new Color32(255, 255, 255, 255);
        }

        private static Color32 ResolveFarViewTilemapTint(Tilemap tilemap, Vector3Int cell, TileBase tile)
        {
            Color32 tint = ResolveFarViewTileTint(tile);
            if (tilemap == null)
                return tint;

            return ApplyFarViewTint((Color32)tilemap.GetColor(cell), tint);
        }

        private static Color32 ApplyFarViewTint(Color32 color, Color32 tint)
        {
            if (tint.r == 255 && tint.g == 255 && tint.b == 255 && tint.a == 255)
                return color;

            return new Color32(
                (byte)((color.r * tint.r + 127) / 255),
                (byte)((color.g * tint.g + 127) / 255),
                (byte)((color.b * tint.b + 127) / 255),
                (byte)((color.a * tint.a + 127) / 255));
        }

        private static Matrix4x4 ResolveFarViewTileTransform(TileBase tile, Matrix4x4 transform)
        {
            if (transform == default || transform == Matrix4x4.identity)
                transform = GetTileTransform(tile);
            return transform;
        }

        private static Color32 CompositeFarViewLayers(Color32 under, Color32 over)
        {
            if (over.a >= 255 || under.a == 0)
                return over;
            if (over.a == 0)
                return under;

            float underA = under.a / 255f;
            float overA = over.a / 255f;
            float outA = overA + (underA * (1f - overA));
            if (outA <= 0f)
                return new Color32(0, 0, 0, 0);

            float outR = ((over.r / 255f) * overA) + ((under.r / 255f) * underA * (1f - overA));
            float outG = ((over.g / 255f) * overA) + ((under.g / 255f) * underA * (1f - overA));
            float outB = ((over.b / 255f) * overA) + ((under.b / 255f) * underA * (1f - overA));
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((outR / outA) * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((outG / outA) * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((outB / outA) * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255));
        }

        private static bool HasFarViewTileSnapshot(TilemapChunkSnapshot snapshot)
        {
            return snapshot != null && snapshot.HasTiles;
        }

        private static bool HasFarViewTilePayload(FarViewChunk chunk)
        {
            var renderSource = chunk != null ? chunk.RenderSource : null;
            return chunk != null
                && renderSource != null
                && (HasFarViewTileSnapshot(renderSource.GroundSnapshot)
                    || HasFarViewTileSnapshot(renderSource.TransitionSnapshot)
                    || HasFarViewTileSnapshot(renderSource.PropSnapshot)
                    || HasFarViewTileSnapshot(renderSource.BlockerSnapshot));
        }

        private bool CanUseFarViewPayloadOnlyPath(FarViewChunk chunk)
        {
            if (chunk == null)
                return false;

            var renderSource = chunk.RenderSource;
            bool hasBackground = ShouldRasterizeFarViewBackground(chunk);
            bool hasGround = HasFarViewTileSnapshot(renderSource.GroundSnapshot);
            bool hasTransitions = HasFarViewTileSnapshot(renderSource.TransitionSnapshot);
            bool hasProps = HasFarViewTileSnapshot(renderSource.PropSnapshot);
            bool hasBlockers = HasFarViewTileSnapshot(renderSource.BlockerSnapshot);
            return hasBackground || hasGround || hasTransitions || hasProps || hasBlockers;
        }

        private void RasterizeFarViewTilemapCell(
            Color32[] pixels,
            int width,
            int height,
            Vector3 captureMin,
            float invWidth,
            float invHeight,
            TilemapChunkCellData cell,
            bool placeUnderExisting,
            ref bool changed)
        {
            if (!cell.HasTile)
                return;
            if (!TryResolveFarViewSpriteSample(cell.Tile, out var sample, out _))
                return;

            Color32 tint = cell.Tint;
            float minX = cell.WorldMin.x;
            float minY = cell.WorldMin.y;
            float maxX = cell.WorldMax.x;
            float maxY = cell.WorldMax.y;
            if (Mathf.Abs(maxX - minX) <= 0.0001f || Mathf.Abs(maxY - minY) <= 0.0001f)
                return;

            int xMin = Mathf.Clamp(Mathf.FloorToInt((minX - captureMin.x) * invWidth * width), 0, width - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt((minY - captureMin.y) * invHeight * height), 0, height - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt((maxX - captureMin.x) * invWidth * width), xMin + 1, width);
            int yMax = Mathf.Clamp(Mathf.CeilToInt((maxY - captureMin.y) * invHeight * height), yMin + 1, height);
            if (xMax <= xMin || yMax <= yMin)
                return;

            int cellWidth = xMax - xMin;
            int cellHeight = yMax - yMin;
            if (cellWidth <= 0 || cellHeight <= 0)
                return;

            var inverseTransform = ResolveFarViewTileTransform(cell.Tile, cell.Transform).inverse;
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
                    var tileColor = sample.Pixels[(sampleY * sample.Width) + sampleX];
                    if (tileColor.a == 0)
                        continue;

                    tileColor = ApplyFarViewTint(tileColor, tint);
                    if (tileColor.a == 0)
                        continue;

                    int pixelIndex = row + x;
                    var composite = placeUnderExisting
                        ? CompositeFarViewLayers(tileColor, pixels[pixelIndex])
                        : CompositeFarViewLayers(pixels[pixelIndex], tileColor);
                    if (!composite.Equals(pixels[pixelIndex]))
                    {
                        pixels[pixelIndex] = composite;
                        changed = true;
                    }
                }
            }
        }

        private void RasterizeFarViewTileSnapshot(
            Color32[] pixels,
            int width,
            int height,
            Vector3 captureMin,
            float invWidth,
            float invHeight,
            TilemapChunkSnapshot snapshot,
            bool placeUnderExisting,
            ref bool changed)
        {
            if (!HasFarViewTileSnapshot(snapshot))
                return;

            BoundsInt bounds = snapshot.CellBounds;
            for (int row = bounds.yMin; row < bounds.yMax; row++)
            {
                for (int col = bounds.xMin; col < bounds.xMax; col++)
                {
                    if (!snapshot.TryGetCell(col, row, out var cell) || !cell.HasTile)
                        continue;

                    RasterizeFarViewTilemapCell(
                        pixels,
                        width,
                        height,
                        captureMin,
                        invWidth,
                        invHeight,
                        cell,
                        placeUnderExisting,
                        ref changed);
                }
            }
        }

        private void CompositeChunkPayloadSources(FarViewChunk chunk, int width, int height)
        {
            var renderSource = chunk.RenderSource;
            bool hasBackground = ShouldRasterizeFarViewBackground(chunk);
            bool hasGround = HasFarViewTileSnapshot(renderSource.GroundSnapshot);
            bool hasTransitions = HasFarViewTileSnapshot(renderSource.TransitionSnapshot);
            bool hasProps = HasFarViewTileSnapshot(renderSource.PropSnapshot);
            bool hasBlockers = HasFarViewTileSnapshot(renderSource.BlockerSnapshot);
            if (!hasBackground && !hasGround && !hasTransitions && !hasProps && !hasBlockers)
                return;

            var pixels = chunk.Texture.GetPixels32();
            bool changed = false;
            float invWidth = 1f / Mathf.Max(0.0001f, chunk.CaptureBounds.size.x);
            float invHeight = 1f / Mathf.Max(0.0001f, chunk.CaptureBounds.size.y);
            Vector3 captureMin = chunk.CaptureBounds.min;

            if (hasBackground)
            {
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
                        placeUnderExisting: true,
                        ref changed);
                }
            }

            RasterizeFarViewTileSnapshot(
                pixels,
                width,
                height,
                captureMin,
                invWidth,
                invHeight,
                renderSource.GroundSnapshot,
                placeUnderExisting: true,
                ref changed);
            RasterizeFarViewTileSnapshot(
                pixels,
                width,
                height,
                captureMin,
                invWidth,
                invHeight,
                renderSource.TransitionSnapshot,
                placeUnderExisting: true,
                ref changed);
            RasterizeFarViewTileSnapshot(
                pixels,
                width,
                height,
                captureMin,
                invWidth,
                invHeight,
                renderSource.PropSnapshot,
                placeUnderExisting: true,
                ref changed);
            RasterizeFarViewTileSnapshot(
                pixels,
                width,
                height,
                captureMin,
                invWidth,
                invHeight,
                renderSource.BlockerSnapshot,
                placeUnderExisting: true,
                ref changed);

            if (!changed)
                return;

            chunk.Texture.SetPixels32(pixels);
            chunk.Texture.Apply(false, false);
        }

        private bool TryUpdateChunkFromPayloadSources(FarViewChunk chunk, int width, int height, float ppu)
        {
            if (!CanUseFarViewPayloadOnlyPath(chunk))
                return false;

            if (TryUpdateChunkFromPayloadSourcesGpu(chunk, width, height, ppu))
                return true;

            var renderSource = chunk.RenderSource;
            bool hasGround = HasFarViewTileSnapshot(renderSource.GroundSnapshot);
            bool hasTransitions = HasFarViewTileSnapshot(renderSource.TransitionSnapshot);
            bool hasProps = HasFarViewTileSnapshot(renderSource.PropSnapshot);
            bool hasBlockers = HasFarViewTileSnapshot(renderSource.BlockerSnapshot);
            if (!hasGround && !hasTransitions && !hasProps && !hasBlockers)
                return TryUpdateChunkFromBackgroundPayload(chunk, width, height, ppu);

            EnsureChunkTexture(chunk, width, height);
            if (chunk.Texture == null)
                return false;

            Color32[] pixels;
            bool changed = false;
            if (TryBuildChunkBackgroundPayloadTexture(chunk, width, height) && chunk.BackgroundTexture != null)
            {
                pixels = chunk.BackgroundTexture.GetPixels32();
                changed = true;
            }
            else
            {
                pixels = new Color32[width * height];
            }

            float invWidth = 1f / Mathf.Max(0.0001f, chunk.CaptureBounds.size.x);
            float invHeight = 1f / Mathf.Max(0.0001f, chunk.CaptureBounds.size.y);
            Vector3 captureMin = chunk.CaptureBounds.min;

            RasterizeFarViewTileSnapshot(
                pixels,
                width,
                height,
                captureMin,
                invWidth,
                invHeight,
                renderSource.GroundSnapshot,
                placeUnderExisting: false,
                ref changed);
            RasterizeFarViewTileSnapshot(
                pixels,
                width,
                height,
                captureMin,
                invWidth,
                invHeight,
                renderSource.TransitionSnapshot,
                placeUnderExisting: false,
                ref changed);
            RasterizeFarViewTileSnapshot(
                pixels,
                width,
                height,
                captureMin,
                invWidth,
                invHeight,
                renderSource.PropSnapshot,
                placeUnderExisting: false,
                ref changed);
            RasterizeFarViewTileSnapshot(
                pixels,
                width,
                height,
                captureMin,
                invWidth,
                invHeight,
                renderSource.BlockerSnapshot,
                placeUnderExisting: false,
                ref changed);

            if (!changed)
                return false;

            chunk.Texture.SetPixels32(pixels);
            chunk.Texture.Apply(false, false);
            EnsureChunkSprite(chunk, width, height, ppu);
            return true;
        }
    }
}
