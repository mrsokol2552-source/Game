/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundConversionSprite.cs
@module: presentation.pathfinding.worldgen.ground_conversion_sprite
@purpose: Hosts runtime square-sprite conversion entry point outside the ProceduralEnvironment monolith.
@entry: PENV-38, ProceduralEnvironment.TryCreateSquareSprite
@api: partial class implementation for ProceduralEnvironment
@deps: ground conversion cache, readable texture cache, raster helpers, and ground tile override settings
@data: generated square ground sprites, runtime textures, sprite cache entries, and dark-edge diagnostics
@perf: hot during runtime terrain palette conversion; keeps sprite warp work isolated from world orchestration
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and terrain generation smoke tests
@config: ground tile crop, manual diamond, unskew, edge trim, dilation, fill, filter, and target-width settings
@assets: source tile sprites and generated runtime square sprites
@notes: conversion sprite orchestration is split from ProceduralEnvironment.cs; low-level pixel helpers live in ProceduralEnvironment.GroundConversionRaster.cs
*/

using System;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-GROUNDCONVERSIONSPRITE]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundConversionSprite.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private bool TryCreateSquareSprite(Sprite sprite, out Sprite result)
        {
            result = null;
            if (sprite == null) return false;
            if (_runtimeGroundSpriteCache.TryGetValue(sprite, out var cached) && cached != null)
            {
                result = cached;
                return true;
            }

            var sourceTexture = sprite.texture;
            if (sourceTexture == null) return false;
            var readable = GetReadableTexture(sourceTexture);
            if (readable == null) return false;

            float insetPixels = GroundTileDiamondInsetPixels;
            int edgeTrimPixels = GroundTileEdgeTrimPixels;
            float edgeBlackThreshold = GroundTileEdgeBlackThreshold;
            float edgeChromaThreshold = GroundTileEdgeChromaThreshold;
            float extraTopInsetPixels = 0f;
            float extraRightInsetPixels = 0f;
            float extraBottomInsetPixels = 0f;
            float extraLeftInsetPixels = 0f;
            ApplyGroundTileOverrides(
                sprite.name,
                ref insetPixels,
                ref edgeTrimPixels,
                ref edgeBlackThreshold,
                ref edgeChromaThreshold,
                ref extraTopInsetPixels,
                ref extraRightInsetPixels,
                ref extraBottomInsetPixels,
                ref extraLeftInsetPixels);

            bool useManualDiamond = UseGroundTileManualDiamond;
            bool debugOutline = useManualDiamond && GroundTileDebugOutlineOnly;
            bool useUnskew = UseGroundTileUnskew || useManualDiamond;
            var rect = sprite.textureRect;
            var cropRect = rect;
            float threshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            if (!debugOutline && !useManualDiamond && UseGroundTileAutoCrop)
            {
                int fullW = Mathf.Max(1, Mathf.RoundToInt(rect.width));
                int fullH = Mathf.Max(1, Mathf.RoundToInt(rect.height));
                var fullPixels = readable.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), fullW, fullH);
                int minPx = fullW;
                int minPy = fullH;
                int maxPx = -1;
                int maxPy = -1;
                for (int y = 0; y < fullH; y++)
                {
                    int row = y * fullW;
                    for (int x = 0; x < fullW; x++)
                    {
                        if (fullPixels[row + x].a <= threshold) continue;
                        if (x < minPx) minPx = x;
                        if (y < minPy) minPy = y;
                        if (x > maxPx) maxPx = x;
                        if (y > maxPy) maxPy = y;
                    }
                }

                if (maxPx >= minPx && maxPy >= minPy)
                {
                    int pad = Mathf.Max(0, GroundTileAutoCropPadding);
                    minPx = Mathf.Max(0, minPx - pad);
                    minPy = Mathf.Max(0, minPy - pad);
                    maxPx = Mathf.Min(fullW - 1, maxPx + pad);
                    maxPy = Mathf.Min(fullH - 1, maxPy + pad);
                    cropRect = new Rect(rect.x + minPx, rect.y + minPy, (maxPx - minPx + 1), (maxPy - minPy + 1));
                }
            }

            if (!debugOutline && !useManualDiamond)
            {
                float minX = Mathf.Clamp01(GroundTileCropMin.x);
                float minY = Mathf.Clamp01(GroundTileCropMin.y);
                float maxX = Mathf.Clamp01(GroundTileCropMax.x);
                float maxY = Mathf.Clamp01(GroundTileCropMax.y);
                if (maxX <= minX) maxX = Mathf.Min(1f, minX + 0.01f);
                if (maxY <= minY) maxY = Mathf.Min(1f, minY + 0.01f);
                cropRect = new Rect(
                    cropRect.x + (cropRect.width * minX),
                    cropRect.y + (cropRect.height * minY),
                    cropRect.width * (maxX - minX),
                    cropRect.height * (maxY - minY));
            }

            int cropW = Mathf.Max(1, Mathf.RoundToInt(cropRect.width));
            int cropH = Mathf.Max(1, Mathf.RoundToInt(cropRect.height));
            var srcPixels = readable.GetPixels(Mathf.RoundToInt(cropRect.x), Mathf.RoundToInt(cropRect.y), cropW, cropH);
            if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)
            {
                srcPixels = UnrotatePackedPixels(srcPixels, cropW, cropH, sprite.packingRotation, out cropW, out cropH);
                cropRect = new Rect(0f, 0f, cropW, cropH);
            }
            if (useManualDiamond)
            {
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
                    cropRect = new Rect(0f, 0f, fullW, fullH);
                }
            }

            float ratio = Mathf.Max(0.01f, GroundTileIsoRatio);
            float resolutionScale = Mathf.Max(0.01f, GroundTileResolutionScale);
            int faceX = 0;
            int faceY = 0;
            int faceW = cropW;
            int faceH = cropH;
            int diamondBoundsX = 0;
            int diamondBoundsY = 0;
            int diamondBoundsW = cropW;
            int diamondBoundsH = cropH;
            if (UseGroundTileUnskew && !useManualDiamond)
            {
                int topRow = -1;
                int maxWidth = 0;
                int rowAtMax = -1;
                int rowMinX = 0;
                int rowMaxX = 0;
                int peakRow = -1;
                int peakWidth = 0;
                int peakMinX = 0;
                int peakMaxX = 0;
                int prevWidth = -1;
                int prevMinX = 0;
                int prevMaxX = 0;
                bool sawIncrease = false;
                for (int y = 0; y < cropH; y++)
                {
                    int rowStart = y * cropW;
                    int minPx = cropW;
                    int maxPx = -1;
                    for (int x = 0; x < cropW; x++)
                    {
                        if (srcPixels[rowStart + x].a <= threshold) continue;
                        if (x < minPx) minPx = x;
                        if (x > maxPx) maxPx = x;
                    }
                    if (maxPx < minPx) continue;
                    if (topRow < 0) topRow = y;
                    int width = maxPx - minPx + 1;
                    if (prevWidth >= 0)
                    {
                        if (width > prevWidth)
                            sawIncrease = true;
                        else if (width < prevWidth && sawIncrease && peakRow < 0)
                        {
                            peakRow = y - 1;
                            peakWidth = prevWidth;
                            peakMinX = prevMinX;
                            peakMaxX = prevMaxX;
                            break;
                        }
                    }
                    if (width > maxWidth)
                    {
                        maxWidth = width;
                        rowAtMax = y;
                        rowMinX = minPx;
                        rowMaxX = maxPx;
                    }
                    prevWidth = width;
                    prevMinX = minPx;
                    prevMaxX = maxPx;
                }

                int useRow = peakRow >= 0 ? peakRow : rowAtMax;
                int useWidth = peakRow >= 0 ? peakWidth : maxWidth;
                int useMinX = peakRow >= 0 ? peakMinX : rowMinX;
                int useMaxX = peakRow >= 0 ? peakMaxX : rowMaxX;
                if (useRow >= 0 && useWidth > 0)
                {
                    int heightToMax = (topRow >= 0) ? (useRow - topRow) : 0;
                    int faceHFromShape = heightToMax > 0 ? (heightToMax * 2 + 1) : 0;
                    int targetH = faceHFromShape > 0 ? faceHFromShape : Mathf.RoundToInt(useWidth / ratio);
                    if (targetH <= 0) targetH = cropH - Mathf.Max(0, topRow);
                    faceH = Mathf.Clamp(targetH, 1, cropH - Mathf.Max(0, topRow));
                    float rowCenterX = (useMinX + useMaxX) * 0.5f;
                    faceW = Mathf.Clamp(useWidth, 1, cropW);
                    faceX = Mathf.RoundToInt(rowCenterX - (faceW - 1) * 0.5f);
                    faceX = Mathf.Clamp(faceX, 0, cropW - faceW);
                    faceY = topRow >= 0 ? topRow : 0;
                }
            }

            Vector2 cornerTop = Vector2.zero;
            Vector2 cornerRight = Vector2.zero;
            Vector2 cornerBottom = Vector2.zero;
            Vector2 cornerLeft = Vector2.zero;
            bool hasDiamond = false;
            bool[] insideDiamond = null;
            if (useManualDiamond)
            {
                float maxX = cropW - 1f;
                float maxY = cropH - 1f;
                float manualMaxX = maxX;
                float manualMaxY = maxY;
                Vector2 top = GroundTileDiamondTop;
                Vector2 right = GroundTileDiamondRight;
                Vector2 bottom = GroundTileDiamondBottom;
                Vector2 left = GroundTileDiamondLeft;
                if (GroundTileDiamondNormalized)
                {
                    top = new Vector2(top.x * manualMaxX, top.y * manualMaxY);
                    right = new Vector2(right.x * manualMaxX, right.y * manualMaxY);
                    bottom = new Vector2(bottom.x * manualMaxX, bottom.y * manualMaxY);
                    left = new Vector2(left.x * manualMaxX, left.y * manualMaxY);
                }
                if (GroundTileDiamondYFromTop)
                {
                    top.y = manualMaxY - top.y;
                    right.y = manualMaxY - right.y;
                    bottom.y = manualMaxY - bottom.y;
                    left.y = manualMaxY - left.y;
                }
                cornerTop = new Vector2(Mathf.Clamp(top.x, 0f, maxX), Mathf.Clamp(top.y, 0f, maxY));
                cornerRight = new Vector2(Mathf.Clamp(right.x, 0f, maxX), Mathf.Clamp(right.y, 0f, maxY));
                cornerBottom = new Vector2(Mathf.Clamp(bottom.x, 0f, maxX), Mathf.Clamp(bottom.y, 0f, maxY));
                cornerLeft = new Vector2(Mathf.Clamp(left.x, 0f, maxX), Mathf.Clamp(left.y, 0f, maxY));
                float inset = Mathf.Max(0f, insetPixels);
                var centroid = (cornerTop + cornerRight + cornerBottom + cornerLeft) * 0.25f;
                if (inset > 0f)
                {
                    cornerTop = InsetCorner(cornerTop, centroid, inset);
                    cornerRight = InsetCorner(cornerRight, centroid, inset);
                    cornerBottom = InsetCorner(cornerBottom, centroid, inset);
                    cornerLeft = InsetCorner(cornerLeft, centroid, inset);
                    centroid = (cornerTop + cornerRight + cornerBottom + cornerLeft) * 0.25f;
                }
                if (extraTopInsetPixels > 0f)
                    cornerTop = InsetCorner(cornerTop, centroid, extraTopInsetPixels);
                if (extraRightInsetPixels > 0f)
                    cornerRight = InsetCorner(cornerRight, centroid, extraRightInsetPixels);
                if (extraBottomInsetPixels > 0f)
                    cornerBottom = InsetCorner(cornerBottom, centroid, extraBottomInsetPixels);
                if (extraLeftInsetPixels > 0f)
                    cornerLeft = InsetCorner(cornerLeft, centroid, extraLeftInsetPixels);
                float manualW = Vector2.Distance(cornerLeft, cornerRight);
                float manualH = Vector2.Distance(cornerTop, cornerBottom);
                if (manualW >= 1f && manualH >= 1f)
                {
                    faceW = Mathf.Max(1, Mathf.RoundToInt(manualW));
                    faceH = Mathf.Max(1, Mathf.RoundToInt(manualH));
                    float minFaceX = Mathf.Min(Mathf.Min(cornerLeft.x, cornerRight.x), Mathf.Min(cornerTop.x, cornerBottom.x));
                    float maxFaceX = Mathf.Max(Mathf.Max(cornerLeft.x, cornerRight.x), Mathf.Max(cornerTop.x, cornerBottom.x));
                    float minFaceY = Mathf.Min(Mathf.Min(cornerLeft.y, cornerRight.y), Mathf.Min(cornerTop.y, cornerBottom.y));
                    float maxFaceY = Mathf.Max(Mathf.Max(cornerLeft.y, cornerRight.y), Mathf.Max(cornerTop.y, cornerBottom.y));
                    diamondBoundsX = Mathf.Clamp(Mathf.FloorToInt(minFaceX), 0, cropW - 1);
                    diamondBoundsY = Mathf.Clamp(Mathf.FloorToInt(minFaceY), 0, cropH - 1);
                    diamondBoundsW = Mathf.Clamp(Mathf.CeilToInt(maxFaceX - minFaceX + 1f), 1, cropW - diamondBoundsX);
                    diamondBoundsH = Mathf.Clamp(Mathf.CeilToInt(maxFaceY - minFaceY + 1f), 1, cropH - diamondBoundsY);
                    faceX = diamondBoundsX;
                    faceY = diamondBoundsY;
                    hasDiamond = true;
                }
                else
                {
                    useManualDiamond = false;
                    useUnskew = UseGroundTileUnskew;
                }
                if (hasDiamond && GroundTileMaskOutsideDiamond)
                {
                    insideDiamond = BuildDiamondMask(cropW, cropH, cornerTop, cornerRight, cornerBottom, cornerLeft);
                }
            }
            if (useManualDiamond && faceH > 0)
            {
                ratio = Mathf.Clamp(faceW / (float)faceH, 0.25f, 4f);
            }

            if (debugOutline && hasDiamond)
            {
                int outW = Mathf.Max(1, cropW);
                int outH = Mathf.Max(1, cropH);
                var outlinePixels = new Color[outW * outH];
                var outlineColor = GroundTileDebugOutlineColor;
                DrawLine(outlinePixels, outW, outH, cornerTop, cornerRight, outlineColor, GroundTileDebugOutlineThickness);
                DrawLine(outlinePixels, outW, outH, cornerRight, cornerBottom, outlineColor, GroundTileDebugOutlineThickness);
                DrawLine(outlinePixels, outW, outH, cornerBottom, cornerLeft, outlineColor, GroundTileDebugOutlineThickness);
                DrawLine(outlinePixels, outW, outH, cornerLeft, cornerTop, outlineColor, GroundTileDebugOutlineThickness);

                var outlineTexture = new Texture2D(outW, outH, TextureFormat.RGBA32, false)
                {
                    filterMode = GroundTileFilterMode,
                    wrapMode = TextureWrapMode.Clamp
                };
                outlineTexture.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                outlineTexture.SetPixels(outlinePixels);
                outlineTexture.Apply(false, false);

                float ppu = Mathf.Max(0.001f, sprite.pixelsPerUnit);
                var pivotNorm = new Vector2(
                    sprite.rect.width > 0f ? (sprite.pivot.x / sprite.rect.width) : 0.5f,
                    sprite.rect.height > 0f ? (sprite.pivot.y / sprite.rect.height) : 0.5f);
                var outlineSprite = Sprite.Create(outlineTexture, new Rect(0, 0, outW, outH), pivotNorm, ppu);
                outlineSprite.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                outlineSprite.name = sprite.name;

                _runtimeGroundObjects.Add(outlineTexture);
                _runtimeGroundObjects.Add(outlineSprite);
                _runtimeGroundSpriteCache[sprite] = outlineSprite;
                result = outlineSprite;
                return true;
            }

            bool preservePattern = useManualDiamond && GroundTilePreservePattern && hasDiamond;
            if (useManualDiamond && GroundTileNoTransform && hasDiamond)
            {
                int outW = Mathf.Max(1, diamondBoundsW);
                int outH = Mathf.Max(1, diamondBoundsH);
                var noWarpPixels = new Color[outW * outH];
                for (int y = 0; y < outH; y++)
                {
                    int sy = diamondBoundsY + y;
                    if (sy < 0 || sy >= cropH) continue;
                    int srcRow = sy * cropW;
                    int dstRow = y * outW;
                    for (int x = 0; x < outW; x++)
                    {
                        int sx = diamondBoundsX + x;
                        if (sx < 0 || sx >= cropW) continue;
                        if (insideDiamond != null && !insideDiamond[srcRow + sx])
                        {
                            noWarpPixels[dstRow + x] = Color.clear;
                            continue;
                        }
                        noWarpPixels[dstRow + x] = srcPixels[srcRow + sx];
                    }
                }

                var noWarpTexture = new Texture2D(outW, outH, TextureFormat.RGBA32, false)
                {
                    filterMode = GroundTileFilterMode,
                    wrapMode = TextureWrapMode.Clamp
                };
                noWarpTexture.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                noWarpTexture.SetPixels(noWarpPixels);
                noWarpTexture.Apply(false, false);

                float noWarpTargetWorldWidth = outW / Mathf.Max(0.001f, sprite.pixelsPerUnit);
                noWarpTargetWorldWidth = ResolveGroundTargetWorldWidth(noWarpTargetWorldWidth);
                float noWarpPpu = outW / Mathf.Max(0.001f, noWarpTargetWorldWidth);
                var noWarpSprite = Sprite.Create(noWarpTexture, new Rect(0, 0, outW, outH), new Vector2(0.5f, 0.5f), noWarpPpu);
                noWarpSprite.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                noWarpSprite.name = sprite.name;

                _runtimeGroundObjects.Add(noWarpTexture);
                _runtimeGroundObjects.Add(noWarpSprite);
                _runtimeGroundSpriteCache[sprite] = noWarpSprite;
                result = noWarpSprite;
                return true;
            }
            int baseSize = preservePattern
                ? Mathf.RoundToInt(Mathf.Max(diamondBoundsW, diamondBoundsH))
                : (useUnskew
                    ? (useManualDiamond ? Mathf.RoundToInt(faceW / ratio) : faceW)
                    : Mathf.RoundToInt(cropW / ratio));
            int outSize = Mathf.RoundToInt(baseSize * resolutionScale);
            if (outSize <= 0)
                outSize = Mathf.Max(1, preservePattern ? Mathf.Max(diamondBoundsW, diamondBoundsH) : (useUnskew ? faceW : cropH));

            var outPixels = new Color[outSize * outSize];
            Color sum = Color.black;
            int count = 0;
            for (int i = 0; i < srcPixels.Length; i++)
            {
                var c = srcPixels[i];
                if (c.a <= threshold) continue;
                sum.r += c.r;
                sum.g += c.g;
                sum.b += c.b;
                count++;
            }
            Color avg = count > 0
                ? new Color(sum.r / count, sum.g / count, sum.b / count, 1f)
                : Color.black;

            float half = (outSize - 1) * 0.5f;
            float invHalf = half > 0.0001f ? 1f / half : 0f;
            float isoW = (useUnskew ? faceW : cropW) - 1f;
            float isoH = (useUnskew ? faceH : cropH) - 1f;
            float centerX = (useUnskew ? faceX : 0) + isoW * 0.5f;
            float centerY = (useUnskew ? faceY : 0) + isoH * 0.5f
                + (GroundTileCenterYOffset * isoH);
            if (!useManualDiamond && UseGroundTileUnskew)
            {
                hasDiamond = TryFindDiamondCorners(faceX, faceY, faceW, faceH, srcPixels, cropW, threshold,
                    out cornerTop, out cornerRight, out cornerBottom, out cornerLeft);
            }

            for (int y = 0; y < outSize; y++)
            {
                float v = (y - half) * invHalf;
                for (int x = 0; x < outSize; x++)
                {
                    float u = (x - half) * invHalf;
                    float sx;
                    float sy;
                    if (preservePattern)
                    {
                        float offsetX = (outSize - diamondBoundsW) * 0.5f;
                        float offsetY = (outSize - diamondBoundsH) * 0.5f;
                        sx = diamondBoundsX + (x - offsetX);
                        sy = diamondBoundsY + (y - offsetY);
                    }
                    else if (useUnskew && hasDiamond)
                    {
                        float uu = outSize > 1 ? (x / (float)(outSize - 1)) : 0f;
                        float vv = outSize > 1 ? (y / (float)(outSize - 1)) : 0f;
                        float invU = 1f - uu;
                        float invV = 1f - vv;
                        float px = (cornerTop.x * invU * invV)
                            + (cornerRight.x * uu * invV)
                            + (cornerBottom.x * uu * vv)
                            + (cornerLeft.x * invU * vv);
                        float py = (cornerTop.y * invU * invV)
                            + (cornerRight.y * uu * invV)
                            + (cornerBottom.y * uu * vv)
                            + (cornerLeft.y * invU * vv);
                        sx = px;
                        sy = py;
                    }
                    else if (useUnskew)
                    {
                        float isoX = (u - v) * 0.25f * isoW;
                        float isoY = (u + v) * 0.25f * isoH;
                        sx = isoX + centerX;
                        sy = isoY + centerY;
                    }
                    else
                    {
                        sx = ((u * 0.5f) + 0.5f) * (cropW - 1f);
                        sy = ((v * 0.5f) + 0.5f) * (cropH - 1f);
                    }

                    int ix = Mathf.RoundToInt(sx);
                    int iy = Mathf.RoundToInt(sy);
                    int sampleX = ix;
                    int sampleY = iy;
                    bool outOfRange = sampleX < 0 || sampleY < 0 || sampleX >= cropW || sampleY >= cropH;
                    bool outsideDiamond = false;
                    bool inside = true;
                    if (insideDiamond != null)
                    {
                        int cx = Mathf.Clamp(sampleX, 0, cropW - 1);
                        int cy = Mathf.Clamp(sampleY, 0, cropH - 1);
                        inside = insideDiamond[(cy * cropW) + cx];
                        outsideDiamond = !inside;
                        if (GroundTileEdgeDilatePixels > 0 && !inside)
                        {
                            if (TryFindNearestOpaque(cx, cy, cropW, cropH, insideDiamond, srcPixels, threshold, GroundTileEdgeDilatePixels, out var nx, out var ny))
                            {
                                sampleX = nx;
                                sampleY = ny;
                                outOfRange = false;
                                outsideDiamond = false;
                                inside = true;
                            }
                        }
                    }
                    Color sp = (outOfRange || outsideDiamond) ? Color.clear : srcPixels[(sampleY * cropW) + sampleX];
                    if (!outOfRange && inside && sp.a <= threshold && GroundTileEdgeDilatePixels > 0)
                    {
                        if (TryFindNearestOpaque(sampleX, sampleY, cropW, cropH, insideDiamond, srcPixels, threshold, GroundTileEdgeDilatePixels, out var nx, out var ny))
                        {
                            sp = srcPixels[(ny * cropW) + nx];
                        }
                    }
                    if (!outOfRange && !outsideDiamond && edgeTrimPixels > 0 && sp.a > threshold)
                    {
                        int edge = edgeTrimPixels;
                        if (x < edge || y < edge || x >= (outSize - edge) || y >= (outSize - edge))
                        {
                            if (IsEdgeDark(sp, edgeBlackThreshold, edgeChromaThreshold))
                            {
                                int cx = Mathf.Clamp(sampleX, 0, cropW - 1);
                                int cy = Mathf.Clamp(sampleY, 0, cropH - 1);
                                if (TryFindNearestOpaque(cx, cy, cropW, cropH, insideDiamond, srcPixels, threshold, edge, out var nx, out var ny, edgeBlackThreshold, edgeChromaThreshold))
                                {
                                    sp = srcPixels[(ny * cropW) + nx];
                                }
                                else
                                {
                                    sp = Color.clear;
                                }
                            }
                        }
                    }
                    bool transparent = outOfRange || outsideDiamond || sp.a <= threshold;
                    if (transparent)
                    {
                        if (!GroundTileFillTransparent || outsideDiamond)
                            continue;
                        sp = avg;
                        sp.a = 1f;
                    }
                    outPixels[(y * outSize) + x] = sp;
                }
            }

            if (GroundTileDebugLogDarkEdges)
            {
                if (HasDarkEdgePixels(outPixels, outSize, outSize, out var edgeRatio))
                {
                    string key = sprite.name;
                    if (_loggedDarkEdgeSprites.Add(key))
                        Debug.LogWarning($"[GroundTileDebug] Dark edge ratio={edgeRatio:0.00} sprite={sprite.name}");
                }
            }

            var outTexture = new Texture2D(outSize, outSize, TextureFormat.RGBA32, false)
            {
                filterMode = GroundTileFilterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            outTexture.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            outTexture.SetPixels(outPixels);
            outTexture.Apply(false, false);

            float targetWorldWidth = (preservePattern ? diamondBoundsW : (useUnskew ? faceW : cropW)) / Mathf.Max(0.001f, sprite.pixelsPerUnit);
            targetWorldWidth = ResolveGroundTargetWorldWidth(targetWorldWidth);
            float newPpu = outSize / Mathf.Max(0.001f, targetWorldWidth);
            var runtimeSprite = Sprite.Create(outTexture, new Rect(0, 0, outSize, outSize), new Vector2(0.5f, 0.5f), newPpu);
            runtimeSprite.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            runtimeSprite.name = sprite.name;

            _runtimeGroundObjects.Add(outTexture);
            _runtimeGroundObjects.Add(runtimeSprite);
            _runtimeGroundSpriteCache[sprite] = runtimeSprite;
            result = runtimeSprite;
            return true;
        }
    }
}
