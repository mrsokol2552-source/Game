/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.TilemapUtilities.cs
@module: presentation.pathfinding.worldgen.tilemap_utilities
@purpose: Hosts tilemap creation, ground fill, sorting layer, grid sizing, and obstacle collider utilities for ProceduralEnvironment.
@entry: PENV-02, ProceduralEnvironment.FindOrCreateTilemap, ProceduralEnvironment.FillGroundTiles, ProceduralEnvironment.ResolveCellSize
@api: partial class implementation for ProceduralEnvironment
@deps: Unity Tilemap/Grid, SortingLayer, HexPathfindingBootstrap obstacle mask
@data: tilemap components, ground fill palettes, obstacle layers, grid cell size
@perf: startup-sensitive and generation hot path for bulk ground fill
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and scene generation smoke tests
@config: tilemap names/orders, sorting layer name, ground noise settings, obstacle layer name, cell size override
@assets: GroundTiles, Background tilemap tiles
@notes: keep Unity object setup and low-level tilemap utilities out of ProceduralEnvironment.cs so render/backend extraction can stay focused
*/

using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-TILEMAPUTILITIES]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.TilemapUtilities.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private Tilemap FindOrCreateTilemap(Transform parent, string name, int sortingOrder)
        {
            if (parent == null || string.IsNullOrEmpty(name)) return null;
            var child = parent.Find(name);
            GameObject go = child != null ? child.gameObject : new GameObject(name);
            if (child == null)
                go.transform.SetParent(parent, false);
            var tm = go.GetComponent<Tilemap>();
            if (tm == null) tm = go.AddComponent<Tilemap>();
            var tr = go.GetComponent<TilemapRenderer>();
            if (tr == null) tr = go.AddComponent<TilemapRenderer>();
            tr.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(SortingLayerName) && SortingLayerExists(SortingLayerName))
                tr.sortingLayerName = SortingLayerName;
            return tm;
        }

        private void FillGroundTiles(Tilemap map, int width, int height, System.Random rng, TileBase[] palette)
        {
            int size = width * height;
            var tiles = new TileBase[size];
            for (int row = 0; row < height; row++)
            {
                int rowBase = row * width;
                for (int col = 0; col < width; col++)
                {
                    tiles[rowBase + col] = PickGroundTile(col, row, rng, palette);
                }
            }
            var bounds = new BoundsInt(0, 0, 0, width, height, 1);
            map.SetTilesBlock(bounds, tiles);
        }

        private void FillGroundTilesBlock(Tilemap map, int width, int startRow, int rowCount, System.Random rng, TileBase[] palette)
        {
            int size = width * rowCount;
            var tiles = new TileBase[size];
            for (int r = 0; r < rowCount; r++)
            {
                int rowBase = r * width;
                int row = startRow + r;
                for (int c = 0; c < width; c++)
                {
                    tiles[rowBase + c] = PickGroundTile(c, row, rng, palette);
                }
            }
            var bounds = new BoundsInt(0, startRow, 0, width, rowCount, 1);
            map.SetTilesBlock(bounds, tiles);
        }

        private void EnsurePropCollider(Tilemap map)
        {
            if (map == null) return;
            var rb = map.GetComponent<Rigidbody2D>();
            if (rb == null) rb = map.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var collider = map.GetComponent<TilemapCollider2D>();
            if (collider == null) collider = map.gameObject.AddComponent<TilemapCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;

            var composite = map.GetComponent<CompositeCollider2D>();
            if (composite == null) composite = map.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        }

        private Vector3 ResolveCellSize()
        {
            if (CellSizeOverride != Vector3.zero)
                return CellSizeOverride;
            if (_hex == null) return Vector3.one;
            float size = Mathf.Max(0.001f, _hex.HexSize);
            if (CellLayout == GridLayout.CellLayout.Hexagon)
                return new Vector3(Mathf.Sqrt(3f) * size, 2f * size, 0f);
            return new Vector3(size, size, 0f);
        }

        private int ResolveObstacleLayer()
        {
            int layer = LayerMask.NameToLayer(ObstacleLayerName);
            if (layer < 0 && _hex != null)
                layer = FirstLayerFromMask(_hex.ObstacleMask);
            if (layer < 0) layer = 0;
            return layer;
        }

        private void ApplyObstacleLayer(Tilemap map)
        {
            if (map == null) return;
            int layer = ResolveObstacleLayer();
            if (layer >= 0)
                map.gameObject.layer = layer;
            if (_hex != null && layer >= 0)
            {
                var m = _hex.ObstacleMask;
                m.value |= (1 << layer);
                _hex.ObstacleMask = m;
            }
        }

        private static TileBase PickTile(System.Random rng, TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return null;
            return palette[rng.Next(palette.Length)];
        }

        private TileBase PickGroundTile(int col, int row, System.Random rng, TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return null;
            if (!UseGroundNoise || GroundNoiseScale <= 0f)
                return PickTile(rng, palette);

            float scale = Mathf.Max(0.0001f, GroundNoiseScale);
            int q = col - (row - (row & 1)) / 2;
            int r = row;
            const float sqrt3Over2 = 0.8660254f;
            float wx = q + (r * 0.5f);
            float wy = r * sqrt3Over2;
            float nx = (wx + _groundNoiseOffset.x) * scale;
            float ny = (wy + _groundNoiseOffset.y) * scale;
            float v = Mathf.Clamp01(Mathf.PerlinNoise(nx, ny));

            if (GroundGroupSize > 0)
            {
                int groupSize = Mathf.Max(1, GroundGroupSize);
                int groupCount = (palette.Length + groupSize - 1) / groupSize;
                int groupIndex = Mathf.Clamp(Mathf.FloorToInt(v * groupCount), 0, groupCount - 1);
                int groupStart = groupIndex * groupSize;
                int groupEnd = Mathf.Min(groupStart + groupSize, palette.Length);
                return palette[rng.Next(groupStart, groupEnd)];
            }

            int idx = Mathf.Clamp(Mathf.FloorToInt(v * palette.Length), 0, palette.Length - 1);
            return palette[idx];
        }

        private static bool SortingLayerExists(string name)
        {
            foreach (var layer in SortingLayer.layers)
            {
                if (layer.name == name) return true;
            }
            return false;
        }

        private static int FirstLayerFromMask(LayerMask mask)
        {
            int m = mask.value;
            if (m == 0) return -1;
            for (int i = 0; i < 32; i++)
            {
                if (((m >> i) & 1) != 0) return i;
            }
            return -1;
        }
    }
}
