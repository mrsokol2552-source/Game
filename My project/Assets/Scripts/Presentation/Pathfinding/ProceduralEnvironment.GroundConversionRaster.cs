/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundConversionRaster.cs
@module: presentation.pathfinding.worldgen.ground_conversion_raster
@purpose: Hosts low-level raster and sprite sampling helpers used by runtime ground square-sprite conversion outside the ProceduralEnvironment monolith.
@entry: PENV-37, ProceduralEnvironment.UnrotatePackedPixels, ProceduralEnvironment.TryFindDiamondCorners
@api: partial class implementation for ProceduralEnvironment
@deps: runtime ground conversion pipeline, tile sprite extraction, and edge/dark-pixel diagnostics
@data: packed sprite pixels, diamond masks, quad tests, nearest-opaque sampling, and edge darkness metrics
@perf: medium-hot; these helpers are used by runtime ground conversion and should stay allocation-conscious
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and terrain generation smoke tests
@config: ground edge trim, dark-edge debug, and manual diamond settings in ProceduralEnvironment
@assets: tile sprites and generated runtime texture pixels
@notes: `TryCreateSquareSprite` lives in ProceduralEnvironment.GroundConversionSprite.cs; this file only owns the reusable helper cluster underneath it
*/

using System;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-GROUNDCONVERSIONRASTER]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundConversionRaster.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private static Color[] UnrotatePackedPixels(Color[] pixels, int width, int height, SpritePackingRotation rotation, out int outW, out int outH)
        {
            outW = width;
            outH = height;
            if (pixels == null || pixels.Length == 0)
                return pixels;
            if (rotation == SpritePackingRotation.None)
                return pixels;

            if (rotation == SpritePackingRotation.Rotate180)
            {
                var rotated = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    int dstRow = (height - 1 - y) * width;
                    for (int x = 0; x < width; x++)
                        rotated[dstRow + (width - 1 - x)] = pixels[row + x];
                }

                return rotated;
            }

            if (rotation == SpritePackingRotation.FlipHorizontal)
            {
                var flipped = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    for (int x = 0; x < width; x++)
                        flipped[row + (width - 1 - x)] = pixels[row + x];
                }

                return flipped;
            }

            if (rotation == SpritePackingRotation.FlipVertical)
            {
                var flipped = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    int dstRow = (height - 1 - y) * width;
                    Array.Copy(pixels, row, flipped, dstRow, width);
                }

                return flipped;
            }

            string rotationName = rotation.ToString();
            bool rotateCcw = rotationName.IndexOf("CCW", StringComparison.OrdinalIgnoreCase) >= 0;
            outW = height;
            outH = width;
            var rotated90 = new Color[outW * outH];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int dstX;
                    int dstY;
                    if (rotateCcw)
                    {
                        dstX = height - 1 - y;
                        dstY = x;
                    }
                    else
                    {
                        dstX = y;
                        dstY = width - 1 - x;
                    }

                    rotated90[(dstY * outW) + dstX] = pixels[row + x];
                }
            }

            return rotated90;
        }

        private static Sprite ExtractTileSprite(TileBase tile)
        {
            if (tile == null)
                return null;
            if (tile is Tile t)
                return t.sprite;
            return null;
        }

        private static Matrix4x4 GetTileTransform(TileBase tile)
        {
            if (tile is Tile t)
                return t.transform;
            return Matrix4x4.identity;
        }

        private static Vector2 InsetCorner(Vector2 corner, Vector2 center, float inset)
        {
            var dir = corner - center;
            float len = dir.magnitude;
            if (len <= 0.0001f)
                return corner;
            float shrink = Mathf.Min(inset, len * 0.5f);
            return corner - (dir / len) * shrink;
        }

        private static bool[] BuildDiamondMask(int width, int height, Vector2 top, Vector2 right, Vector2 bottom, Vector2 left)
        {
            var mask = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                float py = y + 0.5f;
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    mask[(y * width) + x] = IsPointInQuad(new Vector2(px, py), top, right, bottom, left);
                }
            }

            return mask;
        }

        private static bool IsPointInQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float ab = Cross(b - a, p - a);
            float bc = Cross(c - b, p - b);
            float cd = Cross(d - c, p - c);
            float da = Cross(a - d, p - d);
            bool hasNeg = ab < 0f || bc < 0f || cd < 0f || da < 0f;
            bool hasPos = ab > 0f || bc > 0f || cd > 0f || da > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }

        private static void DrawLine(Color[] pixels, int width, int height, Vector2 from, Vector2 to, Color color, int thickness)
        {
            int x0 = Mathf.RoundToInt(from.x);
            int y0 = Mathf.RoundToInt(from.y);
            int x1 = Mathf.RoundToInt(to.x);
            int y1 = Mathf.RoundToInt(to.y);
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Plot(pixels, width, height, x0, y0, color, thickness);
                if (x0 == x1 && y0 == y1)
                    break;
                int e2 = err * 2;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private static void Plot(Color[] pixels, int width, int height, int x, int y, Color color, int thickness)
        {
            if (pixels == null)
                return;
            int radius = Mathf.Max(0, thickness - 1);
            for (int oy = -radius; oy <= radius; oy++)
            {
                int py = y + oy;
                if (py < 0 || py >= height)
                    continue;
                int row = py * width;
                for (int ox = -radius; ox <= radius; ox++)
                {
                    int px = x + ox;
                    if (px < 0 || px >= width)
                        continue;
                    pixels[row + px] = color;
                }
            }
        }

        private static bool TryFindNearestOpaque(
            int x,
            int y,
            int width,
            int height,
            bool[] mask,
            Color[] pixels,
            float threshold,
            int radius,
            out int insideX,
            out int insideY,
            float darkThreshold = -1f,
            float chromaThreshold = -1f)
        {
            insideX = x;
            insideY = y;
            if (pixels == null)
                return false;
            int best = int.MaxValue;
            int r = Mathf.Max(1, radius);
            bool requireNonDark = darkThreshold >= 0f;
            for (int oy = -r; oy <= r; oy++)
            {
                int py = y + oy;
                if (py < 0 || py >= height)
                    continue;
                int row = py * width;
                for (int ox = -r; ox <= r; ox++)
                {
                    int px = x + ox;
                    if (px < 0 || px >= width)
                        continue;
                    int idx = row + px;
                    if (mask != null && !mask[idx])
                        continue;
                    if (pixels[idx].a <= threshold)
                        continue;
                    if (requireNonDark && IsEdgeDark(pixels[idx], darkThreshold, chromaThreshold))
                        continue;
                    int dist = (ox * ox) + (oy * oy);
                    if (dist >= best)
                        continue;
                    best = dist;
                    insideX = px;
                    insideY = py;
                    if (best == 0)
                        return true;
                }
            }

            return best != int.MaxValue;
        }

        private static bool IsDark(Color c, float threshold)
        {
            float luma = (c.r * 0.2126f) + (c.g * 0.7152f) + (c.b * 0.0722f);
            return luma <= threshold;
        }

        private static bool IsEdgeDark(Color c, float lumaThreshold, float chromaThreshold)
        {
            float luma = (c.r * 0.2126f) + (c.g * 0.7152f) + (c.b * 0.0722f);
            if (luma > lumaThreshold)
                return false;
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            float chroma = max - min;
            if (chromaThreshold < 0f)
                return true;
            return chroma <= chromaThreshold;
        }

        private bool HasDarkEdgePixels(Color[] pixels, int width, int height, out float ratio)
        {
            ratio = 0f;
            if (pixels == null || pixels.Length == 0 || width <= 0 || height <= 0)
                return false;
            int edge = Mathf.Max(1, GroundTileEdgeTrimPixels + 1);
            int total = 0;
            int dark = 0;
            float alphaThreshold = Mathf.Clamp01(GroundTileAlphaThreshold);
            float lumaThreshold = Mathf.Clamp01(GroundTileEdgeBlackThreshold);
            float chromaThreshold = Mathf.Clamp01(GroundTileEdgeChromaThreshold);

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                bool yEdge = y < edge || y >= height - edge;
                for (int x = 0; x < width; x++)
                {
                    if (!yEdge && x >= edge && x < width - edge)
                        continue;
                    var c = pixels[row + x];
                    if (c.a <= alphaThreshold)
                        continue;
                    total++;
                    if (IsEdgeDark(c, lumaThreshold, chromaThreshold))
                        dark++;
                }
            }

            if (total == 0)
                return false;
            ratio = dark / (float)total;
            return ratio >= GroundTileDebugDarkEdgeRatio;
        }

        private static bool TryFindDiamondCorners(
            int startX,
            int startY,
            int width,
            int height,
            Color[] pixels,
            int stride,
            float threshold,
            out Vector2 top,
            out Vector2 right,
            out Vector2 bottom,
            out Vector2 left)
        {
            top = right = bottom = left = Vector2.zero;
            int topRow = -1;
            int bottomRow = -1;
            float topX = 0f;
            float bottomX = 0f;
            for (int y = startY; y < startY + height; y++)
            {
                int rowStart = y * stride;
                int minPx = int.MaxValue;
                int maxPx = int.MinValue;
                for (int x = startX; x < startX + width; x++)
                {
                    if (pixels[rowStart + x].a <= threshold)
                        continue;
                    if (x < minPx)
                        minPx = x;
                    if (x > maxPx)
                        maxPx = x;
                }

                if (maxPx < minPx)
                    continue;
                if (topRow < 0)
                {
                    topRow = y;
                    topX = (minPx + maxPx) * 0.5f;
                }

                bottomRow = y;
                bottomX = (minPx + maxPx) * 0.5f;
            }

            if (topRow < 0 || bottomRow < 0)
                return false;

            int leftCol = -1;
            int rightCol = -1;
            float leftY = 0f;
            float rightY = 0f;
            for (int x = startX; x < startX + width; x++)
            {
                int minPy = int.MaxValue;
                int maxPy = int.MinValue;
                for (int y = startY; y < startY + height; y++)
                {
                    if (pixels[(y * stride) + x].a <= threshold)
                        continue;
                    if (y < minPy)
                        minPy = y;
                    if (y > maxPy)
                        maxPy = y;
                }

                if (maxPy < minPy)
                    continue;
                if (leftCol < 0)
                {
                    leftCol = x;
                    leftY = (minPy + maxPy) * 0.5f;
                }

                rightCol = x;
                rightY = (minPy + maxPy) * 0.5f;
            }

            if (leftCol < 0 || rightCol < 0)
                return false;

            top = new Vector2(topX, topRow);
            bottom = new Vector2(bottomX, bottomRow);
            left = new Vector2(leftCol, leftY);
            right = new Vector2(rightCol, rightY);
            return true;
        }
    }
}
