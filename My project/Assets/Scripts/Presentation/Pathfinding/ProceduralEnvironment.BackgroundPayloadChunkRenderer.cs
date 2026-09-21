/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundPayloadChunkRenderer.cs
@module: presentation.pathfinding.worldgen.background_payload_chunk_renderer
@purpose: Provides an experimental shader-facing background renderer that consumes cached/streamed background payload data instead of reading Tilemap state.
@entry: PENV-44, ProceduralEnvironment.RefreshBackgroundPayloadChunkRenderer
@api: partial class implementation for ProceduralEnvironment plus internal validation renderer diagnostics for tests
@deps: cached background render payload, streamed background payloads, background tilemap layout, mesh renderer, hidden texture shader
@data: BackgroundRenderCellDecision payloads, chunked sprite meshes, texture-grouped materials
@perf: experimental and disabled by default; rebuilds whole chunk meshes only when the background payload version changes
@thread: main thread only
@tests: Unity Test Runner PlayMode payload renderer tests, SampleScene smoke tests, and repo audits; visual validation is expected before enabling by default
@config: UseBackgroundPayloadChunkRenderer, BackgroundPayloadChunkRendererChunkSize, BackgroundPayloadChunkRendererMaterial
@assets: Hidden/ProceduralEnvironment/GroundPayloadSprite shader
@notes: tilemap writeback remains authoritative; this validates the background payload seam before production shader migration
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Sprites;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-BACKGROUNDPAYLOADCHUNKRENDERER]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundPayloadChunkRenderer.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private static readonly int BackgroundPayloadMainTexId = Shader.PropertyToID("_MainTex");

        internal readonly struct ValidationChunkRendererDiagnostics
        {
            public readonly bool BackgroundSourceAvailable;
            public readonly bool GroundSourceAvailable;
            public readonly bool BiomeMaskSourceAvailable;
            public readonly bool BackgroundRootActive;
            public readonly bool GroundRootActive;
            public readonly bool BiomeMaskRootActive;
            public readonly int BackgroundChunkCount;
            public readonly int BackgroundQuadCount;
            public readonly int GroundChunkCount;
            public readonly int GroundQuadCount;
            public readonly int BiomeMaskChunkCount;
            public readonly int BiomeMaskQuadCount;

            public ValidationChunkRendererDiagnostics(
                bool backgroundSourceAvailable,
                bool groundSourceAvailable,
                bool biomeMaskSourceAvailable,
                bool backgroundRootActive,
                bool groundRootActive,
                bool biomeMaskRootActive,
                int backgroundChunkCount,
                int backgroundQuadCount,
                int groundChunkCount,
                int groundQuadCount,
                int biomeMaskChunkCount,
                int biomeMaskQuadCount)
            {
                BackgroundSourceAvailable = backgroundSourceAvailable;
                GroundSourceAvailable = groundSourceAvailable;
                BiomeMaskSourceAvailable = biomeMaskSourceAvailable;
                BackgroundRootActive = backgroundRootActive;
                GroundRootActive = groundRootActive;
                BiomeMaskRootActive = biomeMaskRootActive;
                BackgroundChunkCount = backgroundChunkCount;
                BackgroundQuadCount = backgroundQuadCount;
                GroundChunkCount = groundChunkCount;
                GroundQuadCount = groundQuadCount;
                BiomeMaskChunkCount = biomeMaskChunkCount;
                BiomeMaskQuadCount = biomeMaskQuadCount;
            }
        }

        internal ValidationChunkRendererDiagnostics GetValidationChunkRendererDiagnostics()
        {
            return new ValidationChunkRendererDiagnostics(
                HasBackgroundPayloadChunkRendererSource(),
                HasCachedGroundPayloadChunkRendererSource() || HasStreamGroundRenderPayloadSource(includeGround: true, includeTransitions: false),
                HasBiomeMaskChunkRendererSource(),
                _backgroundPayloadChunkRendererRoot != null,
                _groundPayloadChunkRendererRoot != null,
                _biomeMaskChunkRendererRoot != null,
                _backgroundPayloadChunkRendererChunkCount,
                _backgroundPayloadChunkRendererQuadCount,
                _groundPayloadChunkRendererChunkCount,
                _groundPayloadChunkRendererQuadCount,
                _biomeMaskChunkRendererChunkCount,
                _biomeMaskChunkRendererQuadCount);
        }

        private sealed class BackgroundPayloadMeshBuilder
        {
            public readonly Texture2D Texture;
            public readonly List<Vector3> Vertices;
            public readonly List<Vector2> Uvs;
            public readonly List<Color32> Colors;
            public readonly List<int> Triangles;

            public BackgroundPayloadMeshBuilder(Texture2D texture, int capacity)
            {
                Texture = texture;
                int quadCapacity = Mathf.Max(4, capacity * 4);
                int indexCapacity = Mathf.Max(6, capacity * 6);
                Vertices = new List<Vector3>(quadCapacity);
                Uvs = new List<Vector2>(quadCapacity);
                Colors = new List<Color32>(quadCapacity);
                Triangles = new List<int>(indexCapacity);
            }
        }

        private void UpdateBackgroundPayloadChunkRendererLifecycle()
        {
            if (!UseBackgroundPayloadChunkRenderer)
            {
                if (_backgroundPayloadChunkRendererRoot != null)
                    ReleaseBackgroundPayloadChunkRenderer();
                return;
            }

            if (!HasBackgroundPayloadChunkRendererSource())
            {
                if (_backgroundPayloadChunkRendererRoot != null)
                    ReleaseBackgroundPayloadChunkRenderer();
                return;
            }

            int expectedVersion = _cachedBackgroundFarViewPayloadVersion;
            if (_backgroundPayloadChunkRendererRoot == null || _backgroundPayloadChunkRendererVersion != expectedVersion)
                RefreshBackgroundPayloadChunkRenderer();
        }

        private bool HasBackgroundPayloadChunkRendererSource()
        {
            return HasCachedBackgroundPayloadChunkRendererSource() || HasStreamBackgroundPayloadChunkRendererSource();
        }

        private bool HasCachedBackgroundPayloadChunkRendererSource()
        {
            if (_background == null || !UseBackgroundTilemap || _cachedBackgroundRenderWidth <= 0 || _cachedBackgroundRenderHeight <= 0)
                return false;

            if (_hasCachedBackgroundFarViewPayload
                && _cachedBackgroundFarViewPayload != null
                && _cachedBackgroundFarViewPayload.Length == _cachedBackgroundRenderWidth * _cachedBackgroundRenderHeight)
            {
                return true;
            }

            return _hasBackgroundRenderInput;
        }

        private bool HasStreamBackgroundPayloadChunkRendererSource()
        {
            if (!_streamingActive || _background == null || !UseBackgroundTilemap)
                return false;

            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (state.Generated && state.BackgroundPayload != null && state.BackgroundCellCount > 0)
                    return true;
            }

            return false;
        }

        // [PENV-44]
        [ContextMenu("Refresh Background Payload Chunk Renderer")]
        private void RefreshBackgroundPayloadChunkRenderer()
        {
            if (!UseBackgroundPayloadChunkRenderer || !HasBackgroundPayloadChunkRendererSource())
            {
                ReleaseBackgroundPayloadChunkRenderer();
                return;
            }

            Material material = ResolveBackgroundPayloadChunkRendererMaterial();
            if (material == null)
            {
                ReleaseBackgroundPayloadChunkRenderer();
                return;
            }

            ReleaseBackgroundPayloadChunkRenderer();
            _backgroundPayloadChunkRendererRoot = new GameObject("BackgroundPayloadChunkRenderer (Experimental)");
            _backgroundPayloadChunkRendererRoot.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            _backgroundPayloadChunkRendererRoot.transform.position = Vector3.zero;

            if (HasCachedBackgroundPayloadChunkRendererSource())
                BuildCachedBackgroundPayloadChunkMeshes(material);
            else
                BuildStreamBackgroundPayloadChunkMeshes(material);

            if (_backgroundPayloadChunkRendererQuadCount <= 0)
            {
                ReleaseBackgroundPayloadChunkRenderer();
                return;
            }

            _backgroundPayloadChunkRendererVersion = _cachedBackgroundFarViewPayloadVersion;
        }

        private void BuildCachedBackgroundPayloadChunkMeshes(Material material)
        {
            if (!TryGetCachedBackgroundPayloadChunkRendererPayload(out var payload, out var width, out var height))
                return;

            int chunkSize = Mathf.Max(1, BackgroundPayloadChunkRendererChunkSize);
            var origin = Vector2Int.zero;
            for (int row = 0; row < height; row += chunkSize)
            {
                int rowCount = Mathf.Min(chunkSize, height - row);
                for (int col = 0; col < width; col += chunkSize)
                {
                    int colCount = Mathf.Min(chunkSize, width - col);
                    BuildBackgroundPayloadChunkMeshes(payload, width, origin, col, row, colCount, rowCount, material);
                }
            }
        }

        private bool TryGetCachedBackgroundPayloadChunkRendererPayload(
            out BackgroundRenderCellDecision[] payload,
            out int width,
            out int height)
        {
            payload = null;
            width = _cachedBackgroundRenderWidth;
            height = _cachedBackgroundRenderHeight;

            int expectedLength = width * height;
            if (_hasCachedBackgroundFarViewPayload
                && _cachedBackgroundFarViewPayload != null
                && expectedLength > 0
                && _cachedBackgroundFarViewPayload.Length == expectedLength)
            {
                payload = _cachedBackgroundFarViewPayload;
                return true;
            }

            if (!TryBuildCachedBackgroundRenderPayload(requireFarViewIncludeBackground: false, out payload, out _))
                return false;

            width = _cachedBackgroundRenderWidth;
            height = _cachedBackgroundRenderHeight;
            return payload != null && payload.Length == width * height;
        }

        private void BuildStreamBackgroundPayloadChunkMeshes(Material material)
        {
            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (!state.Generated
                    || state.BackgroundPayload == null
                    || state.BackgroundCellCount <= 0
                    || state.BackgroundBounds.size.x <= 0
                    || state.BackgroundBounds.size.y <= 0)
                {
                    continue;
                }

                BuildStreamBackgroundPayloadChunkMeshes(state, material);
            }
        }

        private void BuildStreamBackgroundPayloadChunkMeshes(StreamChunkState state, Material material)
        {
            int chunkSize = Mathf.Max(1, BackgroundPayloadChunkRendererChunkSize);
            var origin = new Vector2Int(state.BackgroundBounds.xMin, state.BackgroundBounds.yMin);
            int width = state.BackgroundBounds.size.x;
            int height = state.BackgroundBounds.size.y;

            for (int row = 0; row < height; row += chunkSize)
            {
                int rowCount = Mathf.Min(chunkSize, height - row);
                for (int col = 0; col < width; col += chunkSize)
                {
                    int colCount = Mathf.Min(chunkSize, width - col);
                    BuildBackgroundPayloadChunkMeshes(
                        state.BackgroundPayload,
                        width,
                        origin,
                        col,
                        row,
                        colCount,
                        rowCount,
                        material);
                }
            }
        }

        private void BuildBackgroundPayloadChunkMeshes(
            BackgroundRenderCellDecision[] payload,
            int payloadWidth,
            Vector2Int origin,
            int localStartCol,
            int localStartRow,
            int colCount,
            int rowCount,
            Material material)
        {
            if (payload == null || payloadWidth <= 0 || colCount <= 0 || rowCount <= 0)
                return;

            var builders = new Dictionary<Texture2D, BackgroundPayloadMeshBuilder>();
            int expectedCells = Mathf.Max(1, colCount * rowCount);

            for (int y = 0; y < rowCount; y++)
            {
                int localRow = localStartRow + y;
                int rowBase = localRow * payloadWidth;
                for (int x = 0; x < colCount; x++)
                {
                    int localCol = localStartCol + x;
                    int payloadIndex = rowBase + localCol;
                    if (payloadIndex < 0 || payloadIndex >= payload.Length)
                        continue;

                    var decision = payload[payloadIndex];
                    AddBackgroundPayloadTileQuad(
                        builders,
                        decision.Tile,
                        ResolveFarViewTileTransform(decision.Tile, decision.Transform),
                        ResolveFarViewTileTint(decision.Tile),
                        decision.Col,
                        decision.Row,
                        expectedCells);
                }
            }

            CreateBackgroundPayloadChunkObjects(origin.x + localStartCol, origin.y + localStartRow, builders, material);
        }

        private void AddBackgroundPayloadTileQuad(
            Dictionary<Texture2D, BackgroundPayloadMeshBuilder> builders,
            TileBase tile,
            Matrix4x4 transform,
            Color32 tint,
            int col,
            int row,
            int expectedCells)
        {
            if (tile == null)
                return;

            Sprite sprite = ExtractTileSprite(tile);
            if (sprite == null || sprite.texture == null)
                return;

            if (!TryGetBackgroundPayloadCellFrame(col, row, out var center, out var cellSize))
                return;

            if (!builders.TryGetValue(sprite.texture, out var builder))
            {
                builder = new BackgroundPayloadMeshBuilder(sprite.texture, expectedCells);
                builders.Add(sprite.texture, builder);
            }

            Vector4 uv = DataUtility.GetOuterUV(sprite);
            AddBackgroundPayloadQuad(
                builder,
                TransformBackgroundPayloadCellPoint(transform, center, cellSize, -0.5f, -0.5f),
                TransformBackgroundPayloadCellPoint(transform, center, cellSize, 0.5f, -0.5f),
                TransformBackgroundPayloadCellPoint(transform, center, cellSize, 0.5f, 0.5f),
                TransformBackgroundPayloadCellPoint(transform, center, cellSize, -0.5f, 0.5f),
                new Vector2(uv.x, uv.y),
                new Vector2(uv.z, uv.y),
                new Vector2(uv.z, uv.w),
                new Vector2(uv.x, uv.w),
                tint);
        }

        private bool TryGetBackgroundPayloadCellFrame(int col, int row, out Vector3 center, out Vector2 size)
        {
            center = default;
            size = default;
            if (_background == null)
                return false;

            Vector3 min = _background.CellToWorld(new Vector3Int(col, row, 0));
            Vector3 max = _background.CellToWorld(new Vector3Int(col + 1, row + 1, 0));
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

        private static Vector3 TransformBackgroundPayloadCellPoint(
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

        private static void AddBackgroundPayloadQuad(
            BackgroundPayloadMeshBuilder builder,
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

        private void CreateBackgroundPayloadChunkObjects(
            int startCol,
            int startRow,
            Dictionary<Texture2D, BackgroundPayloadMeshBuilder> builders,
            Material material)
        {
            foreach (var kvp in builders)
            {
                var builder = kvp.Value;
                if (builder.Vertices.Count == 0)
                    continue;

                CreateBackgroundPayloadChunkObject(startCol, startRow, builder, material);
            }
        }

        private void CreateBackgroundPayloadChunkObject(
            int startCol,
            int startRow,
            BackgroundPayloadMeshBuilder builder,
            Material material)
        {
            var chunk = new GameObject($"BackgroundPayloadChunk {startCol},{startRow} {builder.Texture.name}");
            chunk.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            chunk.transform.SetParent(_backgroundPayloadChunkRendererRoot.transform, false);

            var mesh = new Mesh
            {
                name = $"BackgroundPayloadChunk {startCol},{startRow}",
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
            renderer.sortingOrder = BackgroundSortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (!string.IsNullOrEmpty(SortingLayerName))
                renderer.sortingLayerName = SortingLayerName;

            var properties = new MaterialPropertyBlock();
            properties.SetTexture(BackgroundPayloadMainTexId, builder.Texture);
            renderer.SetPropertyBlock(properties);

            _backgroundPayloadChunkRendererChunkCount++;
            _backgroundPayloadChunkRendererQuadCount += builder.Vertices.Count / 4;
            _backgroundPayloadChunkRendererTextureCount++;
        }

        private Material ResolveBackgroundPayloadChunkRendererMaterial()
        {
            if (BackgroundPayloadChunkRendererMaterial != null)
                return BackgroundPayloadChunkRendererMaterial;

            if (_backgroundPayloadChunkRendererRuntimeMaterial != null)
                return _backgroundPayloadChunkRendererRuntimeMaterial;

            Shader shader = Shader.Find("Hidden/ProceduralEnvironment/GroundPayloadSprite");
            if (shader == null)
                return null;

            _backgroundPayloadChunkRendererRuntimeMaterial = new Material(shader)
            {
                name = "BackgroundPayloadSprite (Runtime)",
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
            };
            return _backgroundPayloadChunkRendererRuntimeMaterial;
        }

        private void ReleaseBackgroundPayloadChunkRenderer()
        {
            if (_backgroundPayloadChunkRendererRoot != null)
            {
                ReleaseBackgroundPayloadChunkRendererMeshes(_backgroundPayloadChunkRendererRoot);
                if (UnityEngine.Application.isPlaying)
                    Destroy(_backgroundPayloadChunkRendererRoot);
                else
                    DestroyImmediate(_backgroundPayloadChunkRendererRoot);
            }

            _backgroundPayloadChunkRendererRoot = null;
            _backgroundPayloadChunkRendererVersion = 0;
            _backgroundPayloadChunkRendererChunkCount = 0;
            _backgroundPayloadChunkRendererQuadCount = 0;
            _backgroundPayloadChunkRendererTextureCount = 0;
        }

        private void ReleaseBackgroundPayloadChunkRendererResources()
        {
            ReleaseBackgroundPayloadChunkRenderer();
            if (_backgroundPayloadChunkRendererRuntimeMaterial != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(_backgroundPayloadChunkRendererRuntimeMaterial);
                else
                    DestroyImmediate(_backgroundPayloadChunkRendererRuntimeMaterial);
            }

            _backgroundPayloadChunkRendererRuntimeMaterial = null;
        }

        private static void ReleaseBackgroundPayloadChunkRendererMeshes(GameObject root)
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
