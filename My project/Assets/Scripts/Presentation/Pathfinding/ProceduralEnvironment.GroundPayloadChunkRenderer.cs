/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundPayloadChunkRenderer.cs
@module: presentation.pathfinding.worldgen.ground_payload_chunk_renderer
@purpose: Provides the first experimental shader-facing ground renderer that consumes cached ground payload data instead of reading Tilemap state.
@entry: PENV-41, ProceduralEnvironment.RefreshGroundPayloadChunkRenderer
@api: partial class implementation for ProceduralEnvironment
@deps: cached ground render payload, tile sprite extraction, ground tilemap layout, mesh renderer, hidden texture shader
@data: committed ground render decisions, chunked sprite meshes, texture-grouped materials
@perf: experimental and disabled by default; rebuilds whole chunk meshes only when the cached payload version changes
@thread: main thread only
@tests: Unity recompilation, Unity Test Runner, and repo audits; visual validation is expected before enabling by default
@config: UseGroundPayloadChunkRenderer, GroundPayloadChunkRendererChunkSize, GroundPayloadChunkRendererMaterial
@assets: Hidden/ProceduralEnvironment/GroundPayloadSprite shader
@notes: tilemap writeback remains authoritative; this is a migration scaffold for shader/render backend validation
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Sprites;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-GROUNDPAYLOADCHUNKRENDERER]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundPayloadChunkRenderer.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private static readonly int GroundPayloadMainTexId = Shader.PropertyToID("_MainTex");

        private sealed class GroundPayloadMeshBuilder
        {
            public readonly Texture2D Texture;
            public readonly string LayerName;
            public readonly int SortingOrder;
            public readonly List<Vector3> Vertices;
            public readonly List<Vector2> Uvs;
            public readonly List<Color32> Colors;
            public readonly List<int> Triangles;

            public GroundPayloadMeshBuilder(Texture2D texture, string layerName, int sortingOrder, int capacity)
            {
                Texture = texture;
                LayerName = layerName;
                SortingOrder = sortingOrder;
                int quadCapacity = Mathf.Max(4, capacity * 4);
                int indexCapacity = Mathf.Max(6, capacity * 6);
                Vertices = new List<Vector3>(quadCapacity);
                Uvs = new List<Vector2>(quadCapacity);
                Colors = new List<Color32>(quadCapacity);
                Triangles = new List<int>(indexCapacity);
            }
        }

        private GameObject _groundPayloadChunkRendererRoot;
        private Material _groundPayloadChunkRendererRuntimeMaterial;
        private int _groundPayloadChunkRendererVersion;
        private int _groundPayloadChunkRendererStreamVersion;
        private int _groundPayloadChunkRendererChunkCount;
        private int _groundPayloadChunkRendererQuadCount;
        private int _groundPayloadChunkRendererTextureCount;

        private void UpdateGroundPayloadChunkRendererLifecycle()
        {
            if (!UseGroundPayloadChunkRenderer)
            {
                if (_groundPayloadChunkRendererRoot != null)
                    ReleaseGroundPayloadChunkRenderer();
                return;
            }

            bool hasCachedPayload = HasCachedGroundPayloadChunkRendererSource();
            bool hasStreamPayload = HasStreamGroundRenderPayloadSource(includeGround: true, includeTransitions: false);
            if (!hasCachedPayload && !hasStreamPayload)
            {
                if (_groundPayloadChunkRendererRoot != null)
                    ReleaseGroundPayloadChunkRenderer();
                return;
            }

            int expectedVersion = hasCachedPayload ? _cachedGroundFarViewPayloadVersion : _groundPayloadChunkRendererStreamVersion;
            if (_groundPayloadChunkRendererRoot == null || _groundPayloadChunkRendererVersion != expectedVersion)
                RefreshGroundPayloadChunkRenderer();
        }

        private bool HasCachedGroundPayloadChunkRendererSource()
        {
            return _ground != null
                && _hasCachedGroundRenderPayload
                && _cachedGroundRenderPayload != null
                && _cachedGroundRenderWidth > 0
                && _cachedGroundRenderHeight > 0;
        }

        private void BumpGroundPayloadChunkRendererStreamVersion()
        {
            unchecked
            {
                _groundPayloadChunkRendererStreamVersion++;
                if (_groundPayloadChunkRendererStreamVersion == 0)
                    _groundPayloadChunkRendererStreamVersion = 1;
            }
        }

        [ContextMenu("Refresh Ground Payload Chunk Renderer")]
        private void RefreshGroundPayloadChunkRenderer()
        {
            bool hasCachedPayload = HasCachedGroundPayloadChunkRendererSource();
            bool hasStreamPayload = HasStreamGroundRenderPayloadSource(includeGround: true, includeTransitions: false);
            if (!UseGroundPayloadChunkRenderer || (!hasCachedPayload && !hasStreamPayload))
            {
                ReleaseGroundPayloadChunkRenderer();
                return;
            }

            Material material = ResolveGroundPayloadChunkRendererMaterial();
            if (material == null)
            {
                ReleaseGroundPayloadChunkRenderer();
                return;
            }

            ReleaseGroundPayloadChunkRenderer();
            _groundPayloadChunkRendererRoot = new GameObject("GroundPayloadChunkRenderer (Experimental)");
            _groundPayloadChunkRendererRoot.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            _groundPayloadChunkRendererRoot.transform.position = Vector3.zero;

            if (hasCachedPayload)
                BuildCachedGroundPayloadChunkMeshes(material);
            else
                BuildStreamGroundPayloadChunkMeshes(material);

            _groundPayloadChunkRendererVersion = hasCachedPayload
                ? _cachedGroundFarViewPayloadVersion
                : _groundPayloadChunkRendererStreamVersion;
        }

        private void BuildCachedGroundPayloadChunkMeshes(Material material)
        {
            int chunkSize = Mathf.Max(1, GroundPayloadChunkRendererChunkSize);
            for (int row = 0; row < _cachedGroundRenderHeight; row += chunkSize)
            {
                int rowCount = Mathf.Min(chunkSize, _cachedGroundRenderHeight - row);
                for (int col = 0; col < _cachedGroundRenderWidth; col += chunkSize)
                {
                    int colCount = Mathf.Min(chunkSize, _cachedGroundRenderWidth - col);
                    BuildGroundPayloadChunkMeshes(col, row, colCount, rowCount, material);
                }
            }
        }

        private void BuildGroundPayloadChunkMeshes(
            int startCol,
            int startRow,
            int colCount,
            int rowCount,
            Material material)
        {
            var groundBuilders = new Dictionary<Texture2D, GroundPayloadMeshBuilder>();
            var transitionBuilders = new Dictionary<Texture2D, GroundPayloadMeshBuilder>();
            int expectedCells = Mathf.Max(1, colCount * rowCount);

            for (int y = 0; y < rowCount; y++)
            {
                int row = startRow + y;
                int rowBase = row * _cachedGroundRenderWidth;
                for (int x = 0; x < colCount; x++)
                {
                    int col = startCol + x;
                    var decision = _cachedGroundRenderPayload[rowBase + col];
                    AddGroundPayloadTileQuad(
                        groundBuilders,
                        decision.GroundTile,
                        col,
                        row,
                        "Ground",
                        GroundSortingOrder,
                        expectedCells);
                    AddGroundPayloadTileQuad(
                        transitionBuilders,
                        decision.TransitionTile,
                        col,
                        row,
                        "Transitions",
                        TransitionSortingOrder,
                        expectedCells);
                }
            }

            CreateGroundPayloadChunkObjects(startCol, startRow, groundBuilders, material);
            CreateGroundPayloadChunkObjects(startCol, startRow, transitionBuilders, material);
        }

        private void BuildStreamGroundPayloadChunkMeshes(Material material)
        {
            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (!state.Generated || state.GroundCells == null || state.GroundCellCount <= 0)
                    continue;

                BuildStreamGroundPayloadChunkMeshes(state, material);
            }
        }

        private void BuildStreamGroundPayloadChunkMeshes(StreamChunkState state, Material material)
        {
            var payloadBounds = state.GroundPayloadBounds;
            if (payloadBounds.size.x <= 0 || payloadBounds.size.y <= 0 || state.GroundCells == null)
                return;

            int payloadWidth = payloadBounds.size.x;
            int payloadHeight = payloadBounds.size.y;
            int chunkSize = Mathf.Max(1, GroundPayloadChunkRendererChunkSize);
            for (int row = 0; row < payloadHeight; row += chunkSize)
            {
                int rowCount = Mathf.Min(chunkSize, payloadHeight - row);
                for (int col = 0; col < payloadWidth; col += chunkSize)
                {
                    int colCount = Mathf.Min(chunkSize, payloadWidth - col);
                    BuildStreamGroundPayloadChunkMeshes(
                        state.GroundCells,
                        payloadWidth,
                        payloadBounds,
                        col,
                        row,
                        colCount,
                        rowCount,
                        material);
                }
            }
        }

        private void BuildStreamGroundPayloadChunkMeshes(
            TilemapChunkCellData[] cells,
            int payloadWidth,
            BoundsInt payloadBounds,
            int localStartCol,
            int localStartRow,
            int colCount,
            int rowCount,
            Material material)
        {
            var builders = new Dictionary<Texture2D, GroundPayloadMeshBuilder>();
            int expectedCells = Mathf.Max(1, colCount * rowCount);
            for (int y = 0; y < rowCount; y++)
            {
                int localRow = localStartRow + y;
                int rowBase = localRow * payloadWidth;
                for (int x = 0; x < colCount; x++)
                {
                    int localCol = localStartCol + x;
                    int cellIndex = rowBase + localCol;
                    if (cellIndex < 0 || cellIndex >= cells.Length)
                        continue;

                    var cell = cells[cellIndex];
                    if (!cell.HasTile)
                        continue;

                    AddGroundPayloadTileQuad(
                        builders,
                        cell.Tile,
                        ResolveFarViewTileTransform(cell.Tile, cell.Transform),
                        cell.Tint,
                        cell.WorldMin,
                        cell.WorldMax,
                        "StreamGround",
                        GroundSortingOrder,
                        expectedCells);
                }
            }

            CreateGroundPayloadChunkObjects(
                payloadBounds.xMin + localStartCol,
                payloadBounds.yMin + localStartRow,
                builders,
                material);
        }

        private void AddGroundPayloadTileQuad(
            Dictionary<Texture2D, GroundPayloadMeshBuilder> builders,
            TileBase tile,
            int col,
            int row,
            string layerName,
            int sortingOrder,
            int expectedCells)
        {
            if (tile == null)
                return;

            Sprite sprite = ExtractTileSprite(tile);
            if (sprite == null || sprite.texture == null)
                return;

            if (!TryGetGroundPayloadCellFrame(col, row, out var center, out var cellSize))
                return;

            AddGroundPayloadTileQuad(
                builders,
                tile,
                ResolveFarViewTileTransform(tile, Matrix4x4.identity),
                ResolveFarViewTileTint(tile),
                center,
                cellSize,
                layerName,
                sortingOrder,
                expectedCells);
        }

        private void AddGroundPayloadTileQuad(
            Dictionary<Texture2D, GroundPayloadMeshBuilder> builders,
            TileBase tile,
            Matrix4x4 transform,
            Color32 tint,
            Vector3 worldMin,
            Vector3 worldMax,
            string layerName,
            int sortingOrder,
            int expectedCells)
        {
            Vector3 center = new Vector3(
                (worldMin.x + worldMax.x) * 0.5f,
                (worldMin.y + worldMax.y) * 0.5f,
                (worldMin.z + worldMax.z) * 0.5f);
            Vector2 size = new Vector2(
                Mathf.Abs(worldMax.x - worldMin.x),
                Mathf.Abs(worldMax.y - worldMin.y));
            if (size.x <= 0f || size.y <= 0f)
                return;

            AddGroundPayloadTileQuad(builders, tile, transform, tint, center, size, layerName, sortingOrder, expectedCells);
        }

        private void AddGroundPayloadTileQuad(
            Dictionary<Texture2D, GroundPayloadMeshBuilder> builders,
            TileBase tile,
            Matrix4x4 transform,
            Color32 tint,
            Vector3 center,
            Vector2 cellSize,
            string layerName,
            int sortingOrder,
            int expectedCells)
        {
            if (tile == null)
                return;

            Sprite sprite = ExtractTileSprite(tile);
            if (sprite == null || sprite.texture == null)
                return;

            if (!builders.TryGetValue(sprite.texture, out var builder))
            {
                builder = new GroundPayloadMeshBuilder(sprite.texture, layerName, sortingOrder, expectedCells);
                builders.Add(sprite.texture, builder);
            }

            Vector4 uv = DataUtility.GetOuterUV(sprite);
            AddGroundPayloadQuad(
                builder,
                TransformGroundPayloadCellPoint(transform, center, cellSize, -0.5f, -0.5f),
                TransformGroundPayloadCellPoint(transform, center, cellSize, 0.5f, -0.5f),
                TransformGroundPayloadCellPoint(transform, center, cellSize, 0.5f, 0.5f),
                TransformGroundPayloadCellPoint(transform, center, cellSize, -0.5f, 0.5f),
                new Vector2(uv.x, uv.y),
                new Vector2(uv.z, uv.y),
                new Vector2(uv.z, uv.w),
                new Vector2(uv.x, uv.w),
                tint);
        }

        private bool TryGetGroundPayloadCellFrame(int col, int row, out Vector3 center, out Vector2 size)
        {
            center = default;
            size = default;
            if (_ground == null)
                return false;

            Vector3 min = _ground.CellToWorld(new Vector3Int(col, row, 0));
            Vector3 max = _ground.CellToWorld(new Vector3Int(col + 1, row + 1, 0));
            float minX = Mathf.Min(min.x, max.x);
            float maxX = Mathf.Max(min.x, max.x);
            float minY = Mathf.Min(min.y, max.y);
            float maxY = Mathf.Max(min.y, max.y);
            if (maxX <= minX || maxY <= minY)
                return false;

            center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            size = new Vector2(maxX - minX, maxY - minY);
            return size.x > 0f && size.y > 0f;
        }

        private static Vector3 TransformGroundPayloadCellPoint(
            Matrix4x4 transform,
            Vector3 center,
            Vector2 cellSize,
            float localX,
            float localY)
        {
            Vector3 transformed = transform.MultiplyPoint3x4(new Vector3(localX, localY, 0f));
            return new Vector3(
                center.x + transformed.x * cellSize.x,
                center.y + transformed.y * cellSize.y,
                center.z);
        }

        private static void AddGroundPayloadQuad(
            GroundPayloadMeshBuilder builder,
            Vector3 bl,
            Vector3 br,
            Vector3 tr,
            Vector3 tl,
            Vector2 uvBl,
            Vector2 uvBr,
            Vector2 uvTr,
            Vector2 uvTl,
            Color32 color)
        {
            int start = builder.Vertices.Count;
            builder.Vertices.Add(bl);
            builder.Vertices.Add(br);
            builder.Vertices.Add(tr);
            builder.Vertices.Add(tl);
            builder.Uvs.Add(uvBl);
            builder.Uvs.Add(uvBr);
            builder.Uvs.Add(uvTr);
            builder.Uvs.Add(uvTl);
            builder.Colors.Add(color);
            builder.Colors.Add(color);
            builder.Colors.Add(color);
            builder.Colors.Add(color);
            builder.Triangles.Add(start);
            builder.Triangles.Add(start + 1);
            builder.Triangles.Add(start + 2);
            builder.Triangles.Add(start);
            builder.Triangles.Add(start + 2);
            builder.Triangles.Add(start + 3);
        }

        private void CreateGroundPayloadChunkObjects(
            int startCol,
            int startRow,
            Dictionary<Texture2D, GroundPayloadMeshBuilder> builders,
            Material material)
        {
            foreach (var kvp in builders)
            {
                var builder = kvp.Value;
                if (builder.Vertices.Count == 0)
                    continue;

                CreateGroundPayloadChunkObject(startCol, startRow, builder, material);
            }
        }

        private void CreateGroundPayloadChunkObject(
            int startCol,
            int startRow,
            GroundPayloadMeshBuilder builder,
            Material material)
        {
            var chunk = new GameObject($"GroundPayloadChunk {builder.LayerName} {startCol},{startRow} {builder.Texture.name}");
            chunk.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            chunk.transform.SetParent(_groundPayloadChunkRendererRoot.transform, false);

            var mesh = new Mesh
            {
                name = $"GroundPayloadChunk {builder.LayerName} {startCol},{startRow}",
                indexFormat = builder.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(builder.Vertices);
            mesh.SetUVs(0, builder.Uvs);
            mesh.SetColors(builder.Colors);
            mesh.SetTriangles(builder.Triangles, 0);
            mesh.RecalculateBounds();

            var filter = chunk.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = chunk.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = builder.SortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (!string.IsNullOrEmpty(SortingLayerName))
                renderer.sortingLayerName = SortingLayerName;

            var properties = new MaterialPropertyBlock();
            properties.SetTexture(GroundPayloadMainTexId, builder.Texture);
            renderer.SetPropertyBlock(properties);

            _groundPayloadChunkRendererChunkCount++;
            _groundPayloadChunkRendererQuadCount += builder.Vertices.Count / 4;
            _groundPayloadChunkRendererTextureCount++;
        }

        private Material ResolveGroundPayloadChunkRendererMaterial()
        {
            if (GroundPayloadChunkRendererMaterial != null)
                return GroundPayloadChunkRendererMaterial;

            if (_groundPayloadChunkRendererRuntimeMaterial != null)
                return _groundPayloadChunkRendererRuntimeMaterial;

            Shader shader = Shader.Find("Hidden/ProceduralEnvironment/GroundPayloadSprite");
            if (shader == null)
                return null;

            _groundPayloadChunkRendererRuntimeMaterial = new Material(shader)
            {
                name = "GroundPayloadSprite (Runtime)",
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
            };
            return _groundPayloadChunkRendererRuntimeMaterial;
        }

        private void ReleaseGroundPayloadChunkRenderer()
        {
            if (_groundPayloadChunkRendererRoot != null)
            {
                ReleaseGroundPayloadChunkRendererMeshes(_groundPayloadChunkRendererRoot);
                if (UnityEngine.Application.isPlaying)
                    Destroy(_groundPayloadChunkRendererRoot);
                else
                    DestroyImmediate(_groundPayloadChunkRendererRoot);
            }

            _groundPayloadChunkRendererRoot = null;
            _groundPayloadChunkRendererVersion = 0;
            _groundPayloadChunkRendererChunkCount = 0;
            _groundPayloadChunkRendererQuadCount = 0;
            _groundPayloadChunkRendererTextureCount = 0;
        }

        private void ReleaseGroundPayloadChunkRendererResources()
        {
            ReleaseGroundPayloadChunkRenderer();
            if (_groundPayloadChunkRendererRuntimeMaterial != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(_groundPayloadChunkRendererRuntimeMaterial);
                else
                    DestroyImmediate(_groundPayloadChunkRendererRuntimeMaterial);
            }

            _groundPayloadChunkRendererRuntimeMaterial = null;
        }

        private static void ReleaseGroundPayloadChunkRendererMeshes(GameObject root)
        {
            if (root == null)
                return;

            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i] != null ? filters[i].sharedMesh : null;
                if (mesh == null)
                    continue;

                filters[i].sharedMesh = null;
                if (UnityEngine.Application.isPlaying)
                    Destroy(mesh);
                else
                    DestroyImmediate(mesh);
            }
        }
    }
}
