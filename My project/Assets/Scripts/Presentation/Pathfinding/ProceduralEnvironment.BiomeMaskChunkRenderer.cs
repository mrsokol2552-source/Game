/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BiomeMaskChunkRenderer.cs
@module: presentation.pathfinding.worldgen.biome_mask_chunk_renderer
@purpose: Extracts water/rock biome masks into typed chunk payloads and mirrors them through an experimental vertex-color mesh renderer.
@entry: PENV-42, ProceduralEnvironment.RefreshBiomeMaskChunkRenderer
@api: partial class implementation for ProceduralEnvironment
@deps: background biome mask extraction, background grid/tilemap layout, mesh renderer, hidden vertex-color shader
@data: typed biome-mask chunk payloads, water/rock cell counts, chunked overlay meshes
@perf: experimental and disabled by default; rebuilds chunk meshes only when biome mask data or streamed chunk state changes
@thread: main thread only
@tests: Unity recompilation, Unity Test Runner, and repo audits; visual validation is expected before using as a migration baseline
@config: UseBiomeMaskChunkRenderer, BiomeMaskChunkRendererChunkSize, BiomeMaskChunkRendererMaterial
@assets: Hidden/ProceduralEnvironment/BiomeMaskColor shader
@notes: this layer must consume extracted BackgroundBiomeData only; do not read live Tilemap tile blocks here
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-BIOMEMASKCHUNKRENDERER]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.BiomeMaskChunkRenderer.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private enum BiomeMaskPayloadKind : byte
        {
            Land,
            Water,
            Rock
        }

        private readonly struct BiomeMaskCellPayload
        {
            public readonly int Col;
            public readonly int Row;
            public readonly BiomeMaskPayloadKind Kind;
            public readonly bool IsWaterInterior;
            public readonly int LandDistance;

            public bool IsRenderable => Kind == BiomeMaskPayloadKind.Water || Kind == BiomeMaskPayloadKind.Rock;

            public BiomeMaskCellPayload(
                int col,
                int row,
                BiomeMaskPayloadKind kind,
                bool isWaterInterior,
                int landDistance)
            {
                Col = col;
                Row = row;
                Kind = kind;
                IsWaterInterior = isWaterInterior;
                LandDistance = landDistance;
            }
        }

        private sealed class BiomeMaskChunkPayload
        {
            private readonly BiomeMaskCellPayload[] _cells;

            public readonly BoundsInt Bounds;
            public readonly int WaterCount;
            public readonly int RockCount;

            public int CellCount => _cells != null ? _cells.Length : 0;
            public int RenderableCount => WaterCount + RockCount;
            public bool HasRenderableCells => RenderableCount > 0;

            public BiomeMaskChunkPayload(
                BoundsInt bounds,
                BiomeMaskCellPayload[] cells,
                int waterCount,
                int rockCount)
            {
                Bounds = bounds;
                _cells = cells;
                WaterCount = waterCount;
                RockCount = rockCount;
            }

            public BiomeMaskCellPayload GetCellAtIndex(int index)
            {
                return _cells[index];
            }
        }

        private GameObject _biomeMaskChunkRendererRoot;
        private Material _biomeMaskChunkRendererRuntimeMaterial;
        private int _biomeMaskChunkRendererVersion;
        private int _biomeMaskChunkRendererDataVersion;
        private int _biomeMaskChunkRendererChunkCount;
        private int _biomeMaskChunkRendererQuadCount;

        private void UpdateBiomeMaskChunkRendererLifecycle()
        {
            if (!UseBiomeMaskChunkRenderer)
            {
                if (_biomeMaskChunkRendererRoot != null)
                    ReleaseBiomeMaskChunkRenderer();
                return;
            }

            if (!HasBiomeMaskChunkRendererSource())
            {
                if (_biomeMaskChunkRendererRoot != null)
                    ReleaseBiomeMaskChunkRenderer();
                return;
            }

            if (_biomeMaskChunkRendererRoot == null || _biomeMaskChunkRendererVersion != _biomeMaskChunkRendererDataVersion)
                RefreshBiomeMaskChunkRenderer();
        }

        private bool HasBiomeMaskChunkRendererSource()
        {
            if (!UseWaterBiome || !HasBackgroundBiomeMaskData())
                return false;

            if (_streamingActive)
                return HasStreamBiomeMaskChunkRendererSource();

            return true;
        }

        private bool HasStreamBiomeMaskChunkRendererSource()
        {
            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (state.Generated && state.BackgroundBounds.size.x > 0 && state.BackgroundBounds.size.y > 0)
                    return true;
            }

            return false;
        }

        private void BumpBiomeMaskChunkRendererDataVersion()
        {
            unchecked
            {
                _biomeMaskChunkRendererDataVersion++;
                if (_biomeMaskChunkRendererDataVersion == 0)
                    _biomeMaskChunkRendererDataVersion = 1;
            }
        }

        // [PENV-42]
        // Renderer-facing biome-mask extraction layer for validating water/rock data without live tilemap reads.
        [ContextMenu("Refresh Biome Mask Chunk Renderer")]
        private void RefreshBiomeMaskChunkRenderer()
        {
            if (!UseBiomeMaskChunkRenderer || !HasBiomeMaskChunkRendererSource())
            {
                ReleaseBiomeMaskChunkRenderer();
                return;
            }

            Material material = ResolveBiomeMaskChunkRendererMaterial();
            if (material == null)
            {
                ReleaseBiomeMaskChunkRenderer();
                return;
            }

            ReleaseBiomeMaskChunkRenderer();
            _biomeMaskChunkRendererRoot = new GameObject("BiomeMaskChunkRenderer (Experimental)");
            _biomeMaskChunkRendererRoot.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            _biomeMaskChunkRendererRoot.transform.position = Vector3.zero;

            if (_streamingActive)
                BuildStreamBiomeMaskChunkMeshes(material);
            else
                BuildCachedBiomeMaskChunkMeshes(material);

            _biomeMaskChunkRendererVersion = _biomeMaskChunkRendererDataVersion;
        }

        private void BuildCachedBiomeMaskChunkMeshes(Material material)
        {
            if (!HasBackgroundBiomeMaskData())
                return;

            int chunkSize = Mathf.Max(1, BiomeMaskChunkRendererChunkSize);
            for (int row = 0; row < _backgroundMaskHeight; row += chunkSize)
            {
                int rowCount = Mathf.Min(chunkSize, _backgroundMaskHeight - row);
                for (int col = 0; col < _backgroundMaskWidth; col += chunkSize)
                {
                    int colCount = Mathf.Min(chunkSize, _backgroundMaskWidth - col);
                    var bounds = new BoundsInt(col, row, 0, colCount, rowCount, 1);
                    if (TryExtractBiomeMaskChunkPayload(bounds, null, out var payload))
                        BuildBiomeMaskChunkMesh(payload, material);
                }
            }
        }

        private void BuildStreamBiomeMaskChunkMeshes(Material material)
        {
            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (!state.Generated || state.BackgroundBounds.size.x <= 0 || state.BackgroundBounds.size.y <= 0)
                    continue;

                BuildStreamBiomeMaskChunkMeshes(state, material);
            }
        }

        private void BuildStreamBiomeMaskChunkMeshes(StreamChunkState state, Material material)
        {
            var bounds = state.BackgroundBounds;
            if (!HasBackgroundBiomeMaskData() || bounds.size.x <= 0 || bounds.size.y <= 0)
                return;

            var chunkData = ExtractStreamChunkWorldData(state);
            int chunkSize = Mathf.Max(1, BiomeMaskChunkRendererChunkSize);
            for (int row = bounds.yMin; row < bounds.yMax; row += chunkSize)
            {
                int rowCount = Mathf.Min(chunkSize, bounds.yMax - row);
                for (int col = bounds.xMin; col < bounds.xMax; col += chunkSize)
                {
                    int colCount = Mathf.Min(chunkSize, bounds.xMax - col);
                    var chunkBounds = new BoundsInt(col, row, 0, colCount, rowCount, 1);
                    if (TryExtractBiomeMaskChunkPayload(chunkBounds, chunkData, out var payload))
                        BuildBiomeMaskChunkMesh(payload, material);
                }
            }
        }

        private bool TryExtractStreamBiomeMaskChunkPayload(StreamChunkState state, out BiomeMaskChunkPayload payload)
        {
            payload = null;
            if (!HasBackgroundBiomeMaskData() || !state.Generated || state.BackgroundBounds.size.x <= 0 || state.BackgroundBounds.size.y <= 0)
                return false;

            var chunkData = ExtractStreamChunkWorldData(state);
            return TryExtractBiomeMaskChunkPayload(state.BackgroundBounds, chunkData, out payload);
        }

        private bool TryExtractBiomeMaskChunkPayload(
            BoundsInt bounds,
            StreamChunkWorldData chunkData,
            out BiomeMaskChunkPayload payload)
        {
            payload = null;
            int width = bounds.size.x;
            int height = bounds.size.y;
            if (!HasBackgroundBiomeMaskData() || width <= 0 || height <= 0)
                return false;

            var cells = new BiomeMaskCellPayload[width * height];
            int waterCount = 0;
            int rockCount = 0;
            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                int row = bounds.yMin + y;
                for (int x = 0; x < width; x++)
                {
                    int col = bounds.xMin + x;
                    var biomeData = ResolveBackgroundBiomeData(chunkData, col, row);
                    var kind = ResolveBiomeMaskPayloadKind(biomeData);
                    if (kind == BiomeMaskPayloadKind.Water)
                        waterCount++;
                    else if (kind == BiomeMaskPayloadKind.Rock)
                        rockCount++;

                    cells[idx++] = new BiomeMaskCellPayload(
                        col,
                        row,
                        kind,
                        biomeData.IsWaterInterior,
                        biomeData.LandDistance);
                }
            }

            payload = new BiomeMaskChunkPayload(bounds, cells, waterCount, rockCount);
            return payload.CellCount > 0;
        }

        private static BiomeMaskPayloadKind ResolveBiomeMaskPayloadKind(BackgroundBiomeData biomeData)
        {
            if (!biomeData.InBounds)
                return BiomeMaskPayloadKind.Land;
            if (biomeData.IsWater)
                return BiomeMaskPayloadKind.Water;
            if (biomeData.IsRock)
                return BiomeMaskPayloadKind.Rock;
            return BiomeMaskPayloadKind.Land;
        }

        private void BuildBiomeMaskChunkMesh(BiomeMaskChunkPayload payload, Material material)
        {
            if (payload == null || !payload.HasRenderableCells)
                return;

            int expectedQuads = Mathf.Max(1, payload.RenderableCount);
            var vertices = new List<Vector3>(expectedQuads * 4);
            var colors = new List<Color32>(expectedQuads * 4);
            var triangles = new List<int>(expectedQuads * 6);

            for (int i = 0; i < payload.CellCount; i++)
            {
                var cell = payload.GetCellAtIndex(i);
                if (!cell.IsRenderable)
                    continue;
                if (!TryGetBiomeMaskCellFrame(cell.Col, cell.Row, out var min, out var max))
                    continue;

                AddBiomeMaskQuad(vertices, colors, triangles, min, max, ResolveBiomeMaskPayloadColor(cell));
            }

            if (vertices.Count == 0)
                return;

            CreateBiomeMaskChunkObject(payload.Bounds.xMin, payload.Bounds.yMin, vertices, colors, triangles, material);
        }

        private Color32 ResolveBiomeMaskPayloadColor(BiomeMaskCellPayload cell)
        {
            return cell.Kind == BiomeMaskPayloadKind.Water
                ? (Color32)BiomeMaskWaterColor
                : (Color32)BiomeMaskRockColor;
        }

        private bool TryGetBiomeMaskCellFrame(int col, int row, out Vector3 min, out Vector3 max)
        {
            min = default;
            max = default;

            Vector3 a;
            Vector3 b;
            if (_background != null)
            {
                a = _background.CellToWorld(new Vector3Int(col, row, 0));
                b = _background.CellToWorld(new Vector3Int(col + 1, row + 1, 0));
            }
            else if (_backgroundGrid != null)
            {
                a = _backgroundGrid.CellToWorld(new Vector3Int(col, row, 0));
                b = _backgroundGrid.CellToWorld(new Vector3Int(col + 1, row + 1, 0));
            }
            else
            {
                a = new Vector3(col, row, 0f);
                b = new Vector3(col + 1f, row + 1f, 0f);
            }

            min = new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
            max = new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));
            if (max.x <= min.x || max.y <= min.y)
                return false;

            return true;
        }

        private static void AddBiomeMaskQuad(
            List<Vector3> vertices,
            List<Color32> colors,
            List<int> triangles,
            Vector3 min,
            Vector3 max,
            Color32 color)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(min.x, min.y, min.z));
            vertices.Add(new Vector3(max.x, min.y, min.z));
            vertices.Add(new Vector3(max.x, max.y, max.z));
            vertices.Add(new Vector3(min.x, max.y, max.z));
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private void CreateBiomeMaskChunkObject(
            int startCol,
            int startRow,
            List<Vector3> vertices,
            List<Color32> colors,
            List<int> triangles,
            Material material)
        {
            var chunk = new GameObject($"BiomeMaskChunk {startCol},{startRow}");
            chunk.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            chunk.transform.SetParent(_biomeMaskChunkRendererRoot.transform, false);

            var mesh = new Mesh
            {
                name = $"BiomeMaskChunk {startCol},{startRow}",
                indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var filter = chunk.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = chunk.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = BackgroundSortingOrder + 1;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (!string.IsNullOrEmpty(SortingLayerName))
                renderer.sortingLayerName = SortingLayerName;

            _biomeMaskChunkRendererChunkCount++;
            _biomeMaskChunkRendererQuadCount += vertices.Count / 4;
        }

        private Material ResolveBiomeMaskChunkRendererMaterial()
        {
            if (BiomeMaskChunkRendererMaterial != null)
                return BiomeMaskChunkRendererMaterial;

            if (_biomeMaskChunkRendererRuntimeMaterial != null)
                return _biomeMaskChunkRendererRuntimeMaterial;

            Shader shader = Shader.Find("Hidden/ProceduralEnvironment/BiomeMaskColor");
            if (shader == null)
                return null;

            _biomeMaskChunkRendererRuntimeMaterial = new Material(shader)
            {
                name = "BiomeMaskColor (Runtime)",
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
            };
            return _biomeMaskChunkRendererRuntimeMaterial;
        }

        private void ReleaseBiomeMaskChunkRenderer()
        {
            if (_biomeMaskChunkRendererRoot != null)
            {
                ReleaseBiomeMaskChunkRendererMeshes(_biomeMaskChunkRendererRoot);
                if (UnityEngine.Application.isPlaying)
                    Destroy(_biomeMaskChunkRendererRoot);
                else
                    DestroyImmediate(_biomeMaskChunkRendererRoot);
            }

            _biomeMaskChunkRendererRoot = null;
            _biomeMaskChunkRendererVersion = 0;
            _biomeMaskChunkRendererChunkCount = 0;
            _biomeMaskChunkRendererQuadCount = 0;
        }

        private void ReleaseBiomeMaskChunkRendererResources()
        {
            ReleaseBiomeMaskChunkRenderer();
            if (_biomeMaskChunkRendererRuntimeMaterial != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(_biomeMaskChunkRendererRuntimeMaterial);
                else
                    DestroyImmediate(_biomeMaskChunkRendererRuntimeMaterial);
            }

            _biomeMaskChunkRendererRuntimeMaterial = null;
        }

        private static void ReleaseBiomeMaskChunkRendererMeshes(GameObject root)
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
