/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundConversion.cs
@module: presentation.pathfinding.worldgen.ground_conversion
@purpose: Hosts runtime ground conversion cache lifecycle and palette conversion entry points outside the ProceduralEnvironment monolith.
@entry: PENV-36, ProceduralEnvironment.ConvertGroundPalette, ProceduralEnvironment.ClearRuntimeGroundConversion
@api: partial class implementation for ProceduralEnvironment
@deps: runtime ground tile/sprite caches, readable texture cache, and terrain palette conversion callers
@data: readable texture copies, generated runtime tiles, conversion hash, and cleanup of generated objects
@perf: hot path during terrain prep; isolates conversion cache lifecycle so later sprite-warp extraction can move independently
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and world-generation smoke tests
@config: runtime ground conversion, crop/diamond/hash settings, and conversion batch size in ProceduralEnvironment
@assets: runtime-generated tiles and textures
@notes: cache/palette conversion orchestration is split from sprite conversion; square-sprite entry point lives in ProceduralEnvironment.GroundConversionSprite.cs
*/

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-GROUNDCONVERSION]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundConversion.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private Texture2D GetReadableTexture(Texture2D source)
        {
            if (source == null)
                return null;
            if (source.isReadable)
                return source;
            if (_readableTextureCache.TryGetValue(source, out var cached) && cached != null)
                return cached;

            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply();
            }
            finally
            {
                if (RenderTexture.active == rt)
                    RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

            _readableTextureCache[source] = readable;
            return readable;
        }

        private void ClearRuntimeGroundConversion()
        {
            if (_runtimeGroundObjects.Count == 0)
            {
                _runtimeGroundTileCache.Clear();
                _runtimeGroundSpriteCache.Clear();
                _runtimeGroundConversionHash = 0;
                _edgeProfileCache.Clear();
                _waterInteriorCache.Clear();
                _waterInteriorScoreCache.Clear();
                _loggedDarkEdgeSprites.Clear();
                return;
            }

            for (int i = 0; i < _runtimeGroundObjects.Count; i++)
            {
                var obj = _runtimeGroundObjects[i];
                if (obj == null)
                    continue;
                if (UnityEngine.Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }

            _runtimeGroundObjects.Clear();
            _runtimeGroundTileCache.Clear();
            _runtimeGroundSpriteCache.Clear();
            _runtimeGroundConversionHash = 0;
            _edgeProfileCache.Clear();
            _waterInteriorCache.Clear();
            _waterInteriorScoreCache.Clear();
            _loggedDarkEdgeSprites.Clear();
        }

        private void ClearReadableTextureCache()
        {
            if (_readableTextureCache.Count == 0 && _farViewBackgroundSpriteCache.Count == 0)
                return;

            foreach (var pair in _readableTextureCache)
            {
                if (pair.Value == null)
                    continue;
                if (UnityEngine.Application.isPlaying)
                    Destroy(pair.Value);
                else
                    DestroyImmediate(pair.Value);
            }

            _readableTextureCache.Clear();
            _edgeProfileCache.Clear();
            _waterInteriorCache.Clear();
            _waterInteriorScoreCache.Clear();
            _loggedDarkEdgeSprites.Clear();
            _farViewBackgroundSpriteCache.Clear();
        }

        private TileBase[] ConvertGroundPalette(TileBase[] palette)
        {
            if (palette == null || palette.Length == 0)
                return palette;
            int hash = ComputeGroundConversionHash(palette);
            if (_runtimeGroundConversionHash != hash)
            {
                ClearRuntimeGroundConversion();
                _runtimeGroundConversionHash = hash;
            }

            var converted = new TileBase[palette.Length];
            for (int i = 0; i < palette.Length; i++)
                converted[i] = GetOrCreateRuntimeGroundTile(palette[i]);
            return converted;
        }

        private IEnumerator ConvertGroundPaletteRoutine(TileBase[] palette, Action<TileBase[]> onDone)
        {
            if (palette == null || palette.Length == 0)
            {
                onDone?.Invoke(palette);
                yield break;
            }

            int hash = ComputeGroundConversionHash(palette);
            if (_runtimeGroundConversionHash != hash)
            {
                ClearRuntimeGroundConversion();
                _runtimeGroundConversionHash = hash;
            }

            var converted = new TileBase[palette.Length];
            int batch = Mathf.Max(1, GroundTileConversionBatchSize);
            for (int i = 0; i < palette.Length; i++)
            {
                converted[i] = GetOrCreateRuntimeGroundTile(palette[i]);
                if ((i + 1) % batch == 0)
                    yield return null;
            }

            onDone?.Invoke(converted);
        }

        private int ComputeGroundConversionHash(TileBase[] palette)
        {
            unchecked
            {
                int h = 17;
                h = (h * 23) + (ConvertGroundTilesRuntime ? 1 : 0);
                h = (h * 23) + (UseGroundTileUnskew ? 1 : 0);
                h = (h * 23) + (UseGroundTileManualDiamond ? 1 : 0);
                h = (h * 23) + (GroundTileDiamondNormalized ? 1 : 0);
                h = (h * 23) + (GroundTileDiamondYFromTop ? 1 : 0);
                h = (h * 23) + GroundTileDiamondInsetPixels.GetHashCode();
                h = (h * 23) + (GroundTileMaskOutsideDiamond ? 1 : 0);
                h = (h * 23) + GroundTileEdgeDilatePixels;
                h = (h * 23) + GroundTileEdgeTrimPixels;
                h = (h * 23) + GroundTileEdgeBlackThreshold.GetHashCode();
                h = (h * 23) + GroundTileEdgeChromaThreshold.GetHashCode();
                h = (h * 23) + (GroundTilePreservePattern ? 1 : 0);
                h = (h * 23) + (GroundTileNoTransform ? 1 : 0);
                h = (h * 23) + (GroundTileDebugOutlineOnly ? 1 : 0);
                h = (h * 23) + GroundTileDebugOutlineColor.GetHashCode();
                h = (h * 23) + GroundTileDebugOutlineThickness;
                h = (h * 23) + GroundTileDiamondTop.GetHashCode();
                h = (h * 23) + GroundTileDiamondRight.GetHashCode();
                h = (h * 23) + GroundTileDiamondBottom.GetHashCode();
                h = (h * 23) + GroundTileDiamondLeft.GetHashCode();
                h = (h * 23) + (UseGroundTileAutoCrop ? 1 : 0);
                h = (h * 23) + GroundTileAutoCropPadding;
                h = (h * 23) + GroundTileCropMin.GetHashCode();
                h = (h * 23) + GroundTileCropMax.GetHashCode();
                h = (h * 23) + GroundTileIsoRatio.GetHashCode();
                h = (h * 23) + GroundTileCenterYOffset.GetHashCode();
                h = (h * 23) + GroundTileResolutionScale.GetHashCode();
                h = (h * 23) + GroundTileAlphaThreshold.GetHashCode();
                h = (h * 23) + (GroundTileFillTransparent ? 1 : 0);
                h = (h * 23) + (int)GroundTileFilterMode;
                if (GroundTileOverrides != null)
                {
                    h = (h * 23) + GroundTileOverrides.Length;
                    for (int i = 0; i < GroundTileOverrides.Length; i++)
                    {
                        var ov = GroundTileOverrides[i];
                        if (ov == null)
                            continue;
                        string name = ov.NameContains ?? string.Empty;
                        h = (h * 23) + StringComparer.OrdinalIgnoreCase.GetHashCode(name);
                        h = (h * 23) + ov.ExtraInsetPixels.GetHashCode();
                        h = (h * 23) + ov.ExtraEdgeTrimPixels;
                        h = (h * 23) + (ov.OverrideEdgeBlackThreshold ? 1 : 0);
                        h = (h * 23) + ov.EdgeBlackThresholdOverride.GetHashCode();
                        h = (h * 23) + (ov.OverrideEdgeChromaThreshold ? 1 : 0);
                        h = (h * 23) + ov.EdgeChromaThresholdOverride.GetHashCode();
                        h = (h * 23) + ov.ExtraTopInsetPixels.GetHashCode();
                        h = (h * 23) + ov.ExtraRightInsetPixels.GetHashCode();
                        h = (h * 23) + ov.ExtraBottomInsetPixels.GetHashCode();
                        h = (h * 23) + ov.ExtraLeftInsetPixels.GetHashCode();
                    }
                }

                h = (h * 23) + palette.Length;
                for (int i = 0; i < palette.Length; i++)
                {
                    var tile = palette[i];
                    h = (h * 23) + (tile == null ? 0 : tile.GetInstanceID());
                }

                return h;
            }
        }

        private TileBase GetOrCreateRuntimeGroundTile(TileBase source)
        {
            if (source == null)
                return null;
            if (_runtimeGroundTileCache.TryGetValue(source, out var cached) && cached != null)
                return cached;

            var sprite = ExtractTileSprite(source);
            if (sprite == null)
            {
                _runtimeGroundTileCache[source] = source;
                return source;
            }

            if (!TryCreateSquareSprite(sprite, out var convertedSprite))
            {
                _runtimeGroundTileCache[source] = source;
                return source;
            }

            var runtimeTile = ScriptableObject.CreateInstance<Tile>();
            runtimeTile.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            runtimeTile.name = source.name;
            runtimeTile.sprite = convertedSprite;
            if (source is Tile srcTile)
            {
                runtimeTile.color = srcTile.color;
                runtimeTile.transform = srcTile.transform;
                runtimeTile.flags = srcTile.flags;
                runtimeTile.colliderType = srcTile.colliderType;
            }

            _runtimeGroundObjects.Add(runtimeTile);
            _runtimeGroundTileCache[source] = runtimeTile;
            return runtimeTile;
        }

        private float ResolveGroundTargetWorldWidth(float fallbackWorldWidth)
        {
            if (!GroundTileUseGridCellWidth)
                return fallbackWorldWidth;
            float cellWidth = _grid != null ? _grid.cellSize.x : ResolveCellSize().x;
            float scale = Mathf.Max(0.01f, GroundTileWorldScaleMultiplier);
            float result = cellWidth * scale;
            return result > 0.0001f ? result : fallbackWorldWidth;
        }
    }
}
