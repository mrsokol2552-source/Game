/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.TileProfile.cs
@module: presentation.pathfinding.worldgen.tile_profile
@purpose: Hosts sprite-derived tile edge/water profile extraction and water-interior scoring outside the ProceduralEnvironment monolith.
@entry: PENV-43, ProceduralEnvironment.GetEdgeProfile, ProceduralEnvironment.IsWaterInteriorTile
@api: partial class implementation for ProceduralEnvironment
@deps: readable texture cache, tile sprite extraction, tile data primitives, and water/green edge sampling settings
@data: cached edge profiles, water interior flags/scores, edge water masks, and rotated/mirrored profile transforms
@perf: medium-hot; profile extraction is cached per sprite and reused by variant construction and water edge refinement
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and terrain generation smoke tests
@config: ground edge green thresholds, water edge thresholds, mask samples, water interior thresholds, and sample insets
@assets: tile sprites and readable runtime texture copies
@notes: this layer owns raw sprite profile extraction; TileSelection consumes these profiles for deterministic tile picks
*/

using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-TILEPROFILE]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.TileProfile.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private EdgeProfile GetEdgeProfile(TileBase tile)
        {
            if (tile == null) return default;
            var sprite = ExtractTileSprite(tile);
            if (sprite == null) return default;
            if (_edgeProfileCache.TryGetValue(sprite, out var cached))
                return cached;

            var profile = BuildEdgeProfile(sprite);
            _edgeProfileCache[sprite] = profile;
            return profile;
        }

        private EdgeProfile BuildEdgeProfile(Sprite sprite)
        {
            var profile = new EdgeProfile();
            if (sprite == null) return profile;
            var texture = sprite.texture;
            if (texture == null) return profile;
            var readable = GetReadableTexture(texture);
            if (readable == null) return profile;

            var rect = sprite.textureRect;
            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0) return profile;

            var pixels = readable.GetPixels(x0, y0, width, height);
            if (pixels == null || pixels.Length == 0) return profile;

            int inset = Mathf.Max(0, GroundTileEdgeSampleInsetPixels);
            int minX = Mathf.Clamp(inset, 0, width - 1);
            int maxX = Mathf.Clamp(width - 1 - inset, 0, width - 1);
            int minY = Mathf.Clamp(inset, 0, height - 1);
            int maxY = Mathf.Clamp(height - 1 - inset, 0, height - 1);

            profile.BottomGreen = IsEdgeGreen(pixels, width, minY, minX, maxX, true);
            profile.TopGreen = IsEdgeGreen(pixels, width, maxY, minX, maxX, true);
            profile.LeftGreen = IsEdgeGreen(pixels, width, minX, minY, maxY, false);
            profile.RightGreen = IsEdgeGreen(pixels, width, maxX, minY, maxY, false);

            int waterInset = Mathf.Max(0, WaterEdgeSampleInsetPixels);
            int waterMinX = Mathf.Clamp(waterInset, 0, width - 1);
            int waterMaxX = Mathf.Clamp(width - 1 - waterInset, 0, width - 1);
            int waterMinY = Mathf.Clamp(waterInset, 0, height - 1);
            int waterMaxY = Mathf.Clamp(height - 1 - waterInset, 0, height - 1);
            if (waterMaxX >= waterMinX && waterMaxY >= waterMinY)
            {
                int band = Mathf.Max(1, WaterEdgeSampleBandPixels);
                int bottomMaxY = Mathf.Min(waterMinY + band - 1, waterMaxY);
                int topMinY = Mathf.Max(waterMaxY - band + 1, waterMinY);
                int leftMaxX = Mathf.Min(waterMinX + band - 1, waterMaxX);
                int rightMinX = Mathf.Max(waterMaxX - band + 1, waterMinX);

                profile.BottomWaterRatio = GetEdgeWaterRatioBand(pixels, width, waterMinX, waterMaxX, waterMinY, bottomMaxY);
                profile.TopWaterRatio = GetEdgeWaterRatioBand(pixels, width, waterMinX, waterMaxX, topMinY, waterMaxY);
                profile.LeftWaterRatio = GetEdgeWaterRatioBand(pixels, width, waterMinX, leftMaxX, waterMinY, waterMaxY);
                profile.RightWaterRatio = GetEdgeWaterRatioBand(pixels, width, rightMinX, waterMaxX, waterMinY, waterMaxY);

                profile.BottomWater = profile.BottomWaterRatio >= WaterEdgeBlueRatio;
                profile.TopWater = profile.TopWaterRatio >= WaterEdgeBlueRatio;
                profile.LeftWater = profile.LeftWaterRatio >= WaterEdgeBlueRatio;
                profile.RightWater = profile.RightWaterRatio >= WaterEdgeBlueRatio;

                int samples = Mathf.Clamp(WaterEdgeMaskSamples, 1, 8);
                profile.WaterMaskSamples = (byte)samples;
                profile.BottomWaterMask = BuildWaterEdgeMask(pixels, width, waterMinX, waterMaxX, waterMinY, bottomMaxY, samples, true);
                profile.TopWaterMask = BuildWaterEdgeMask(pixels, width, waterMinX, waterMaxX, topMinY, waterMaxY, samples, true);
                profile.LeftWaterMask = BuildWaterEdgeMask(pixels, width, waterMinX, leftMaxX, waterMinY, waterMaxY, samples, false);
                profile.RightWaterMask = BuildWaterEdgeMask(pixels, width, rightMinX, waterMaxX, waterMinY, waterMaxY, samples, false);
                profile.BottomWaterTransitions = CountMaskTransitions(profile.BottomWaterMask, samples);
                profile.TopWaterTransitions = CountMaskTransitions(profile.TopWaterMask, samples);
                profile.LeftWaterTransitions = CountMaskTransitions(profile.LeftWaterMask, samples);
                profile.RightWaterTransitions = CountMaskTransitions(profile.RightWaterMask, samples);
            }

            return profile;
        }

        private static EdgeProfile RotateProfileCCW(EdgeProfile profile, int steps)
        {
            int s = ((steps % 4) + 4) % 4;
            int samples = Mathf.Clamp(profile.WaterMaskSamples, 1, 8);
            if (s == 0) return profile;
            if (s == 1)
            {
                return new EdgeProfile
                {
                    TopGreen = profile.RightGreen,
                    RightGreen = profile.BottomGreen,
                    BottomGreen = profile.LeftGreen,
                    LeftGreen = profile.TopGreen,
                    TopWater = profile.RightWater,
                    RightWater = profile.BottomWater,
                    BottomWater = profile.LeftWater,
                    LeftWater = profile.TopWater,
                    TopWaterRatio = profile.RightWaterRatio,
                    RightWaterRatio = profile.BottomWaterRatio,
                    BottomWaterRatio = profile.LeftWaterRatio,
                    LeftWaterRatio = profile.TopWaterRatio,
                    TopWaterMask = ReverseMaskBits(profile.RightWaterMask, samples),
                    RightWaterMask = profile.BottomWaterMask,
                    BottomWaterMask = ReverseMaskBits(profile.LeftWaterMask, samples),
                    LeftWaterMask = profile.TopWaterMask,
                    WaterMaskSamples = profile.WaterMaskSamples,
                    TopWaterTransitions = profile.RightWaterTransitions,
                    RightWaterTransitions = profile.BottomWaterTransitions,
                    BottomWaterTransitions = profile.LeftWaterTransitions,
                    LeftWaterTransitions = profile.TopWaterTransitions
                };
            }
            if (s == 2)
            {
                return new EdgeProfile
                {
                    TopGreen = profile.BottomGreen,
                    RightGreen = profile.LeftGreen,
                    BottomGreen = profile.TopGreen,
                    LeftGreen = profile.RightGreen,
                    TopWater = profile.BottomWater,
                    RightWater = profile.LeftWater,
                    BottomWater = profile.TopWater,
                    LeftWater = profile.RightWater,
                    TopWaterRatio = profile.BottomWaterRatio,
                    RightWaterRatio = profile.LeftWaterRatio,
                    BottomWaterRatio = profile.TopWaterRatio,
                    LeftWaterRatio = profile.RightWaterRatio,
                    TopWaterMask = ReverseMaskBits(profile.BottomWaterMask, samples),
                    RightWaterMask = ReverseMaskBits(profile.LeftWaterMask, samples),
                    BottomWaterMask = ReverseMaskBits(profile.TopWaterMask, samples),
                    LeftWaterMask = ReverseMaskBits(profile.RightWaterMask, samples),
                    WaterMaskSamples = profile.WaterMaskSamples,
                    TopWaterTransitions = profile.BottomWaterTransitions,
                    RightWaterTransitions = profile.LeftWaterTransitions,
                    BottomWaterTransitions = profile.TopWaterTransitions,
                    LeftWaterTransitions = profile.RightWaterTransitions
                };
            }
            return new EdgeProfile
            {
                TopGreen = profile.LeftGreen,
                RightGreen = profile.TopGreen,
                BottomGreen = profile.RightGreen,
                LeftGreen = profile.BottomGreen,
                TopWater = profile.LeftWater,
                RightWater = profile.TopWater,
                BottomWater = profile.RightWater,
                LeftWater = profile.BottomWater,
                TopWaterRatio = profile.LeftWaterRatio,
                RightWaterRatio = profile.TopWaterRatio,
                BottomWaterRatio = profile.RightWaterRatio,
                LeftWaterRatio = profile.BottomWaterRatio,
                TopWaterMask = profile.LeftWaterMask,
                RightWaterMask = ReverseMaskBits(profile.TopWaterMask, samples),
                BottomWaterMask = profile.RightWaterMask,
                LeftWaterMask = ReverseMaskBits(profile.BottomWaterMask, samples),
                WaterMaskSamples = profile.WaterMaskSamples,
                TopWaterTransitions = profile.LeftWaterTransitions,
                RightWaterTransitions = profile.TopWaterTransitions,
                BottomWaterTransitions = profile.RightWaterTransitions,
                LeftWaterTransitions = profile.BottomWaterTransitions
            };
        }

        private static EdgeProfile MirrorProfileX(EdgeProfile profile)
        {
            int samples = Mathf.Clamp(profile.WaterMaskSamples, 1, 8);
            return new EdgeProfile
            {
                TopGreen = profile.TopGreen,
                RightGreen = profile.LeftGreen,
                BottomGreen = profile.BottomGreen,
                LeftGreen = profile.RightGreen,
                TopWater = profile.TopWater,
                RightWater = profile.LeftWater,
                BottomWater = profile.BottomWater,
                LeftWater = profile.RightWater,
                TopWaterRatio = profile.TopWaterRatio,
                RightWaterRatio = profile.LeftWaterRatio,
                BottomWaterRatio = profile.BottomWaterRatio,
                LeftWaterRatio = profile.RightWaterRatio,
                TopWaterMask = ReverseMaskBits(profile.TopWaterMask, samples),
                RightWaterMask = profile.LeftWaterMask,
                BottomWaterMask = ReverseMaskBits(profile.BottomWaterMask, samples),
                LeftWaterMask = profile.RightWaterMask,
                WaterMaskSamples = profile.WaterMaskSamples,
                TopWaterTransitions = profile.TopWaterTransitions,
                RightWaterTransitions = profile.LeftWaterTransitions,
                BottomWaterTransitions = profile.BottomWaterTransitions,
                LeftWaterTransitions = profile.RightWaterTransitions
            };
        }

        private static EdgeProfile MirrorProfileY(EdgeProfile profile)
        {
            int samples = Mathf.Clamp(profile.WaterMaskSamples, 1, 8);
            return new EdgeProfile
            {
                TopGreen = profile.BottomGreen,
                RightGreen = profile.RightGreen,
                BottomGreen = profile.TopGreen,
                LeftGreen = profile.LeftGreen,
                TopWater = profile.BottomWater,
                RightWater = profile.RightWater,
                BottomWater = profile.TopWater,
                LeftWater = profile.LeftWater,
                TopWaterRatio = profile.BottomWaterRatio,
                RightWaterRatio = profile.RightWaterRatio,
                BottomWaterRatio = profile.TopWaterRatio,
                LeftWaterRatio = profile.LeftWaterRatio,
                TopWaterMask = profile.BottomWaterMask,
                RightWaterMask = ReverseMaskBits(profile.RightWaterMask, samples),
                BottomWaterMask = profile.TopWaterMask,
                LeftWaterMask = ReverseMaskBits(profile.LeftWaterMask, samples),
                WaterMaskSamples = profile.WaterMaskSamples,
                TopWaterTransitions = profile.BottomWaterTransitions,
                RightWaterTransitions = profile.RightWaterTransitions,
                BottomWaterTransitions = profile.TopWaterTransitions,
                LeftWaterTransitions = profile.LeftWaterTransitions
            };
        }

        private bool IsEdgeGreen(Color[] pixels, int width, int fixedIndex, int from, int to, bool horizontal)
        {
            int total = 0;
            int green = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            for (int i = from; i <= to; i++)
            {
                int x = horizontal ? i : fixedIndex;
                int y = horizontal ? fixedIndex : i;
                int idx = (y * width) + x;
                var c = pixels[idx];
                if (c.a <= alphaThreshold) continue;
                total++;
                if (IsGreenDominant(c))
                    green++;
            }

            if (total <= 0) return false;
            float ratio = green / (float)total;
            return ratio >= GroundTileEdgeGreenRatio;
        }

        private bool IsGreenDominant(Color c)
        {
            float g = c.g;
            if (g < GroundTileEdgeGreenMin) return false;
            float maxOther = Mathf.Max(c.r, c.b);
            return (g - maxOther) >= GroundTileEdgeGreenDominance;
        }

        private bool IsWaterInteriorTile(TileBase tile)
        {
            if (tile == null) return false;
            var sprite = ExtractTileSprite(tile);
            if (sprite == null) return false;
            if (_waterInteriorCache.TryGetValue(sprite, out var cached))
                return cached;

            bool result = BuildWaterInteriorProfile(sprite);
            _waterInteriorCache[sprite] = result;
            return result;
        }

        private bool BuildWaterInteriorProfile(Sprite sprite)
        {
            if (sprite == null) return false;
            var texture = sprite.texture;
            if (texture == null) return false;
            var readable = GetReadableTexture(texture);
            if (readable == null) return false;

            var rect = sprite.textureRect;
            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0) return false;

            var pixels = readable.GetPixels(x0, y0, width, height);
            if (pixels == null || pixels.Length == 0) return false;

            int inset = Mathf.Max(0, WaterEdgeSampleInsetPixels);
            int minX = Mathf.Clamp(inset, 0, width - 1);
            int maxX = Mathf.Clamp(width - 1 - inset, 0, width - 1);
            int minY = Mathf.Clamp(inset, 0, height - 1);
            int maxY = Mathf.Clamp(height - 1 - inset, 0, height - 1);
            if (maxX < minX || maxY < minY) return false;

            int band = Mathf.Max(1, WaterEdgeSampleBandPixels);
            int bottomMaxY = Mathf.Min(minY + band - 1, maxY);
            int topMinY = Mathf.Max(maxY - band + 1, minY);
            int leftMaxX = Mathf.Min(minX + band - 1, maxX);
            int rightMinX = Mathf.Max(maxX - band + 1, minX);

            bool bottomWater = GetEdgeWaterRatioBand(pixels, width, minX, maxX, minY, bottomMaxY) >= WaterEdgeBlueRatio;
            bool topWater = GetEdgeWaterRatioBand(pixels, width, minX, maxX, topMinY, maxY) >= WaterEdgeBlueRatio;
            bool leftWater = GetEdgeWaterRatioBand(pixels, width, minX, leftMaxX, minY, maxY) >= WaterEdgeBlueRatio;
            bool rightWater = GetEdgeWaterRatioBand(pixels, width, rightMinX, maxX, minY, maxY) >= WaterEdgeBlueRatio;
            if (!(bottomWater && topWater && leftWater && rightWater)) return false;

            int interiorInset = Mathf.Max(0, WaterInteriorSampleInsetPixels);
            int ix0 = Mathf.Clamp(interiorInset, 0, width - 1);
            int ix1 = Mathf.Clamp(width - 1 - interiorInset, 0, width - 1);
            int iy0 = Mathf.Clamp(interiorInset, 0, height - 1);
            int iy1 = Mathf.Clamp(height - 1 - interiorInset, 0, height - 1);
            if (ix1 < ix0 || iy1 < iy0) return false;

            int total = 0;
            int water = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            for (int y = iy0; y <= iy1; y++)
            {
                int row = y * width;
                for (int x = ix0; x <= ix1; x++)
                {
                    var c = pixels[row + x];
                    if (c.a <= alphaThreshold) continue;
                    total++;
                    if (IsWaterDominant(c))
                        water++;
                }
            }

            if (total <= 0) return false;
            float ratio = water / (float)total;
            return ratio >= WaterInteriorBlueRatio;
        }

        private bool IsEdgeWater(Color[] pixels, int width, int fixedIndex, int from, int to, bool horizontal)
        {
            int total = 0;
            int water = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            for (int i = from; i <= to; i++)
            {
                int x = horizontal ? i : fixedIndex;
                int y = horizontal ? fixedIndex : i;
                int idx = (y * width) + x;
                var c = pixels[idx];
                if (c.a <= alphaThreshold) continue;
                total++;
                if (IsWaterDominant(c))
                    water++;
            }

            if (total <= 0) return false;
            float ratio = water / (float)total;
            return ratio >= WaterEdgeBlueRatio;
        }

        private float GetEdgeWaterRatioBand(Color[] pixels, int width, int minX, int maxX, int minY, int maxY)
        {
            if (minX > maxX || minY > maxY) return 0f;
            int total = 0;
            int water = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            for (int y = minY; y <= maxY; y++)
            {
                int row = y * width;
                for (int x = minX; x <= maxX; x++)
                {
                    var c = pixels[row + x];
                    if (c.a <= alphaThreshold) continue;
                    total++;
                    if (IsWaterDominant(c))
                        water++;
                }
            }

            if (total <= 0) return 0f;
            float ratio = water / (float)total;
            return ratio;
        }

        private byte BuildWaterEdgeMask(Color[] pixels, int width, int minX, int maxX, int minY, int maxY, int samples, bool horizontal)
        {
            if (minX > maxX || minY > maxY) return 0;
            samples = Mathf.Clamp(samples, 1, 8);
            int length = horizontal ? (maxX - minX + 1) : (maxY - minY + 1);
            if (length <= 0) return 0;
            float threshold = Mathf.Clamp01(WaterEdgeMaskRatioThreshold);
            byte mask = 0;
            for (int s = 0; s < samples; s++)
            {
                float t0 = s / (float)samples;
                float t1 = (s + 1) / (float)samples;
                int segStart = Mathf.FloorToInt(t0 * length);
                int segEnd = Mathf.FloorToInt(t1 * length) - 1;
                if (segEnd < segStart) segEnd = segStart;

                float ratio;
                if (horizontal)
                {
                    int x0 = Mathf.Clamp(minX + segStart, minX, maxX);
                    int x1 = Mathf.Clamp(minX + segEnd, minX, maxX);
                    ratio = GetEdgeWaterRatioBand(pixels, width, x0, x1, minY, maxY);
                }
                else
                {
                    int y0 = Mathf.Clamp(minY + segStart, minY, maxY);
                    int y1 = Mathf.Clamp(minY + segEnd, minY, maxY);
                    ratio = GetEdgeWaterRatioBand(pixels, width, minX, maxX, y0, y1);
                }

                if (ratio >= threshold)
                    mask |= (byte)(1 << s);
            }
            return mask;
        }

        private static byte ReverseMaskBits(byte mask, int samples)
        {
            samples = Mathf.Clamp(samples, 1, 8);
            byte reversed = 0;
            for (int i = 0; i < samples; i++)
            {
                if ((mask & (1 << i)) != 0)
                    reversed |= (byte)(1 << (samples - 1 - i));
            }
            return reversed;
        }

        private static int CountMaskDifference(byte a, byte b, int samples)
        {
            samples = Mathf.Clamp(samples, 1, 8);
            byte diff = (byte)(a ^ b);
            int count = 0;
            for (int i = 0; i < samples; i++)
            {
                if ((diff & (1 << i)) != 0)
                    count++;
            }
            return count;
        }

        private static byte CountMaskTransitions(byte mask, int samples)
        {
            samples = Mathf.Clamp(samples, 1, 8);
            int transitions = 0;
            int prev = (mask & 1) != 0 ? 1 : 0;
            for (int i = 1; i < samples; i++)
            {
                int bit = (mask & (1 << i)) != 0 ? 1 : 0;
                if (bit != prev)
                    transitions++;
                prev = bit;
            }
            return (byte)transitions;
        }

        private bool IsWaterDominant(Color c)
        {
            float b = c.b;
            if (b < WaterEdgeBlueMin) return false;
            float maxOther = Mathf.Max(c.r, c.g);
            return (b - maxOther) >= WaterEdgeBlueDominance;
        }

        private float GetWaterInteriorScore(TileBase tile)
        {
            if (tile == null) return 0f;
            var sprite = ExtractTileSprite(tile);
            if (sprite == null) return 0f;
            if (_waterInteriorScoreCache.TryGetValue(sprite, out var cached))
                return cached;
            float score = BuildWaterInteriorScore(sprite);
            _waterInteriorScoreCache[sprite] = score;
            return score;
        }

        private float BuildWaterInteriorScore(Sprite sprite)
        {
            if (sprite == null) return 0f;
            var texture = sprite.texture;
            if (texture == null) return 0f;
            var readable = GetReadableTexture(texture);
            if (readable == null) return 0f;

            var rect = sprite.textureRect;
            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0) return 0f;

            var pixels = readable.GetPixels(x0, y0, width, height);
            if (pixels == null || pixels.Length == 0) return 0f;

            int inset = Mathf.Max(0, WaterInteriorSampleInsetPixels);
            int ix0 = Mathf.Clamp(inset, 0, width - 1);
            int ix1 = Mathf.Clamp(width - 1 - inset, 0, width - 1);
            int iy0 = Mathf.Clamp(inset, 0, height - 1);
            int iy1 = Mathf.Clamp(height - 1 - inset, 0, height - 1);
            if (ix1 < ix0 || iy1 < iy0) return 0f;

            int total = 0;
            int water = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            for (int y = iy0; y <= iy1; y++)
            {
                int row = y * width;
                for (int x = ix0; x <= ix1; x++)
                {
                    var c = pixels[row + x];
                    if (c.a <= alphaThreshold) continue;
                    total++;
                    if (IsWaterDominant(c))
                        water++;
                }
            }

            if (total <= 0) return 0f;
            return water / (float)total;
        }
    }
}
