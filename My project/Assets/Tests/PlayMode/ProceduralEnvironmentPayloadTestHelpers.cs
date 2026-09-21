using System;
using System.Collections;
using System.Reflection;
using Game.Presentation.Pathfinding;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.Tilemaps;

using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    public partial class ProceduralEnvironmentFarViewPayloadSourceTests
    {
        private static ProceduralEnvironment CreateEnvironment(out GameObject go)
        {
            go = new GameObject("ProceduralEnvironmentFarViewPayloadSourceTest");
            return go.AddComponent<ProceduralEnvironment>();
        }

        private static void AddStreamChunkWithPayloads(ProceduralEnvironment env)
        {
            Type stateType = GetNestedType("StreamChunkState");
            object state = Activator.CreateInstance(stateType);
            SetPrivateField(state, "Generated", true);
            SetPrivateField(state, "BackgroundPayload", CreateNestedArray("BackgroundRenderCellDecision", 1));
            SetPrivateField(state, "BackgroundCellCount", 1);
            SetPrivateField(state, "GroundCells", CreateNestedArray("TilemapChunkCellData", 1));
            SetPrivateField(state, "GroundCellCount", 1);
            SetPrivateField(state, "PropPlacements", CreateNestedList("Placement"));

            var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
            chunks.Add(Vector2Int.zero, state);
        }

        private static Array CreateNestedArray(string nestedTypeName, int length)
        {
            return Array.CreateInstance(GetNestedType(nestedTypeName), length);
        }

        private static object CreateNestedList(string nestedTypeName)
        {
            Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(GetNestedType(nestedTypeName));
            return Activator.CreateInstance(listType);
        }

        private static Array CreateNestedListArray(string nestedTypeName, params object[] lists)
        {
            Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(GetNestedType(nestedTypeName));
            Array array = Array.CreateInstance(listType, lists.Length);
            for (int i = 0; i < lists.Length; i++)
                array.SetValue(lists[i], i);
            return array;
        }

        private static object CreatePlacement(int col, int row, TileBase tile)
        {
            object placement = Activator.CreateInstance(GetNestedType("Placement"));
            SetPrivateField(placement, "Cell", new Vector2Int(col, row));
            SetPrivateField(placement, "Tile", tile);
            return placement;
        }

        private static object CreateGroundRenderCellDecision(
            int globalIndex,
            int col,
            int row,
            TileBase groundTile,
            TileBase transitionTile)
        {
            object decision = Activator.CreateInstance(GetNestedType("GroundRenderCellDecision"));
            SetPrivateField(decision, "GlobalIndex", globalIndex);
            SetPrivateField(decision, "Col", col);
            SetPrivateField(decision, "Row", row);
            SetPrivateField(decision, "LayerIndex", 0);
            SetPrivateField(decision, "GroundTile", groundTile);
            SetPrivateField(decision, "TransitionTile", transitionTile);
            return decision;
        }

        private static object CreateBackgroundRenderCellDecision(
            int globalIndex,
            int col,
            int row,
            TileBase tile,
            Matrix4x4 transform)
        {
            object decision = Activator.CreateInstance(GetNestedType("BackgroundRenderCellDecision"));
            SetPrivateField(decision, "GlobalIndex", globalIndex);
            SetPrivateField(decision, "Col", col);
            SetPrivateField(decision, "Row", row);
            SetPrivateField(decision, "LayerIndex", 0);
            SetPrivateField(decision, "Tile", tile);
            SetPrivateField(decision, "Transform", transform);
            return decision;
        }

        private static bool TryExtractPlacementRenderChunkSnapshots(
            ProceduralEnvironment env,
            Bounds worldBounds,
            out object propSnapshot,
            out object blockerSnapshot)
        {
            object[] args = { worldBounds, null, null };
            bool extracted = (bool)InvokePrivateMethodWithArgs(env, "TryExtractPlacementRenderChunkSnapshots", args);
            propSnapshot = args[1];
            blockerSnapshot = args[2];
            return extracted;
        }

        private static bool TryGetGroundRenderPayloadBounds(
            ProceduralEnvironment env,
            bool includeGround,
            bool includeTransitions,
            out Bounds bounds)
        {
            object[] args = { includeGround, includeTransitions, default(Bounds) };
            bool built = (bool)InvokePrivateMethodWithArgs(env, "TryGetGroundRenderPayloadBounds", args);
            bounds = (Bounds)args[2];
            return built;
        }

        private static bool TryGetCachedPlacementRenderBounds(
            ProceduralEnvironment env,
            bool includeProps,
            bool includeBlockers,
            out Bounds bounds)
        {
            object[] args = { includeProps, includeBlockers, default(Bounds) };
            bool built = (bool)InvokePrivateMethodWithArgs(env, "TryGetCachedPlacementRenderBounds", args);
            bounds = (Bounds)args[2];
            return built;
        }

        private static bool TryBuildFarViewTileSnapshotBounds(ProceduralEnvironment env, out Bounds bounds)
        {
            object[] args = { default(Bounds) };
            bool built = (bool)InvokePrivateMethodWithArgs(env, "TryBuildFarViewTileSnapshotBounds", args);
            bounds = (Bounds)args[0];
            return built;
        }

        private static bool TryBuildFarViewBakeSource(ProceduralEnvironment env, out object source)
        {
            object[] args = { null };
            bool built = (bool)InvokePrivateMethodWithArgs(env, "TryBuildFarViewBakeSource", args);
            source = args[0];
            return built;
        }

        private static bool TryBuildFarViewBackgroundChunkSource(
            ProceduralEnvironment env,
            object bakeSource,
            object reuseChunk,
            Bounds chunkBounds,
            out object source)
        {
            object[] args =
            {
                bakeSource,
                reuseChunk,
                chunkBounds,
                Activator.CreateInstance(GetNestedType("FarViewBackgroundChunkSource"))
            };
            bool built = (bool)InvokePrivateMethodWithArgs(env, "TryBuildFarViewBackgroundChunkSource", args);
            source = args[3];
            return built;
        }

        private static bool TryBuildStreamBackgroundFarViewPayload(
            ProceduralEnvironment env,
            out Array payload,
            out Bounds bounds,
            out int width,
            out int height,
            out Vector2Int origin)
        {
            object[] args =
            {
                null,
                default(Bounds),
                0,
                0,
                default(Vector2Int)
            };
            bool built = (bool)InvokePrivateMethodWithArgs(env, "TryBuildStreamBackgroundFarViewPayload", args);
            payload = (Array)args[0];
            bounds = (Bounds)args[1];
            width = (int)args[2];
            height = (int)args[3];
            origin = (Vector2Int)args[4];
            return built;
        }

        private static void ApplyFarViewBackgroundChunkSource(object chunk, object source)
        {
            InvokePrivateStaticMethod("ApplyFarViewBackgroundChunkSource", chunk, source);
        }

        private static bool TryBuildFarViewTileChunkSource(
            ProceduralEnvironment env,
            Bounds chunkBounds,
            out object source)
        {
            return TryBuildFarViewTileChunkSource(env, null, chunkBounds, out source);
        }

        private static bool TryBuildFarViewTileChunkSource(
            ProceduralEnvironment env,
            object reuseChunk,
            Bounds chunkBounds,
            out object source)
        {
            object[] args =
            {
                reuseChunk,
                chunkBounds,
                Activator.CreateInstance(GetNestedType("FarViewTileChunkSource"))
            };
            bool built = (bool)InvokePrivateMethodWithArgs(env, "TryBuildFarViewTileChunkSource", args);
            source = args[2];
            return built;
        }

        private static object CreateFarViewChunk()
        {
            return Activator.CreateInstance(GetNestedType("FarViewChunk"));
        }

        private static void ApplyFarViewTileChunkSource(
            ProceduralEnvironment env,
            object chunk,
            object source,
            Bounds chunkBounds)
        {
            InvokePrivateMethodWithArgs(env, "ApplyFarViewTileChunkSource", chunk, source, chunkBounds);
        }

        private static void AssertTilemapChunkSnapshot(
            object snapshot,
            string expectedTilemapName,
            BoundsInt expectedCellBounds,
            int expectedCol,
            int expectedRow,
            TileBase expectedTile,
            Color32 expectedTint,
            Vector3 expectedWorldMin,
            Vector3 expectedWorldMax,
            Matrix4x4 expectedTransform)
        {
            Assert.NotNull(snapshot, "Expected placement snapshot.");
            Assert.AreEqual(expectedTilemapName, (string)GetPrivateField(snapshot, "TilemapName"));
            Assert.AreEqual(expectedCellBounds, (BoundsInt)GetPrivateField(snapshot, "CellBounds"));
            Assert.AreEqual(1, (int)GetPrivateField(snapshot, "NonEmptyCount"));
            Assert.True((bool)GetPrivateField(snapshot, "HasGridConfig"), "Expected placement snapshot to preserve grid metadata.");
            Assert.True((bool)GetPrivateField(snapshot, "HasTilemapTransform"), "Expected placement snapshot to preserve tilemap transform metadata.");
            Assert.True(TryGetSnapshotCell(snapshot, expectedCol, expectedRow, out var cell), "Expected placement cell in snapshot.");
            Assert.AreEqual(expectedCol, (int)GetPrivateField(cell, "Col"));
            Assert.AreEqual(expectedRow, (int)GetPrivateField(cell, "Row"));
            Assert.AreSame(expectedTile, (TileBase)GetPrivateField(cell, "Tile"));
            AssertColorEquals(expectedTint, (Color32)GetPrivateField(cell, "Tint"), "placement tint");
            AssertVectorApproximately(expectedWorldMin, (Vector3)GetPrivateField(cell, "WorldMin"), "placement world min");
            AssertVectorApproximately(expectedWorldMax, (Vector3)GetPrivateField(cell, "WorldMax"), "placement world max");
            AssertMatrixApproximately(expectedTransform, (Matrix4x4)GetPrivateField(cell, "Transform"), "placement transform");
        }

        private static Texture2D CreateSolidTexture(Color32 color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels32(new[] { color, color, color, color });
            texture.Apply(false, false);
            return texture;
        }

        private static bool TryGetSnapshotCell(object snapshot, int col, int row, out object cell)
        {
            object[] args = { col, row, Activator.CreateInstance(GetNestedType("TilemapChunkCellData")) };
            bool hasCell = (bool)InvokeInstanceMethod(snapshot, "TryGetCell", args);
            cell = args[2];
            return hasCell;
        }

        private static Bounds CreateCellBounds(int startCol, int startRow, int width, int height)
        {
            return new Bounds(
                new Vector3(startCol + (width * 0.5f), startRow + (height * 0.5f), 0f),
                new Vector3(width, height, 0f));
        }

        private static Vector3[] CreateCellQuadVertices(int startCol, int startRow, int width, int height)
        {
            var vertices = new Vector3[width * height * 4];
            int idx = 0;
            for (int row = startRow; row < startRow + height; row++)
            {
                for (int col = startCol; col < startCol + width; col++)
                {
                    vertices[idx++] = new Vector3(col, row, 0f);
                    vertices[idx++] = new Vector3(col + 1f, row, 0f);
                    vertices[idx++] = new Vector3(col + 1f, row + 1f, 0f);
                    vertices[idx++] = new Vector3(col, row + 1f, 0f);
                }
            }

            return vertices;
        }

        private static Color32[] RepeatColor(Color32 color, int count)
        {
            var colors = new Color32[count];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = color;
            return colors;
        }

        private static Mesh GetMeshForRendererTexture(GameObject root, Texture expectedTexture, out MeshRenderer renderer)
        {
            renderer = null;
            Assert.NotNull(root, "Expected renderer root.");
            Assert.NotNull(expectedTexture, "Expected texture.");

            var block = new MaterialPropertyBlock();
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                block.Clear();
                renderers[i].GetPropertyBlock(block);
                if (block.GetTexture("_MainTex") != expectedTexture)
                    continue;

                renderer = renderers[i];
                Assert.NotNull(renderer.sharedMaterial, $"Expected renderer material for texture {expectedTexture.name}.");
                Assert.AreEqual(
                    "Hidden/ProceduralEnvironment/GroundPayloadSprite",
                    renderer.sharedMaterial.shader.name,
                    $"Expected payload sprite shader for texture {expectedTexture.name}.");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode);
                Assert.False(renderer.receiveShadows);
                var filter = renderer.GetComponent<MeshFilter>();
                Assert.NotNull(filter, $"Expected mesh filter for renderer texture {expectedTexture.name}.");
                Assert.NotNull(filter.sharedMesh, $"Expected mesh data for renderer texture {expectedTexture.name}.");
                return filter.sharedMesh;
            }

            Assert.Fail($"Expected renderer property block to reference texture {expectedTexture.name}.");
            return null;
        }

        private static Mesh GetMeshForChildNamePrefix(GameObject root, string expectedNamePrefix)
        {
            Assert.NotNull(root, "Expected renderer root.");
            var filters = root.GetComponentsInChildren<MeshFilter>();
            for (int i = 0; i < filters.Length; i++)
            {
                if (!filters[i].name.StartsWith(expectedNamePrefix, StringComparison.Ordinal))
                    continue;

                Assert.NotNull(filters[i].sharedMesh, $"Expected mesh data for {expectedNamePrefix}.");
                return filters[i].sharedMesh;
            }

            Assert.Fail($"Expected mesh child with name prefix {expectedNamePrefix}.");
            return null;
        }

        private static void AssertPayloadMesh(
            Mesh mesh,
            Bounds expectedBounds,
            Color32 expectedTint,
            Vector3[] expectedVertices)
        {
            Assert.NotNull(mesh, "Expected generated mesh data.");
            Assert.AreEqual(expectedVertices.Length, mesh.vertexCount);
            Assert.AreEqual((expectedVertices.Length / 4) * 6, mesh.triangles.Length);
            AssertVectorApproximately(expectedBounds.center, mesh.bounds.center, "mesh bounds center");
            AssertVectorApproximately(expectedBounds.size, mesh.bounds.size, "mesh bounds size");
            AssertVectorArrayApproximately(expectedVertices, mesh.vertices, "mesh vertices");
            Assert.AreEqual(expectedVertices.Length, mesh.uv.Length, "Expected one UV per payload vertex.");

            var colors = mesh.colors32;
            Assert.AreEqual(expectedVertices.Length, colors.Length, "Expected one tint color per vertex.");
            for (int i = 0; i < colors.Length; i++)
                AssertColorEquals(expectedTint, colors[i], $"mesh color {i}");
        }

        private static void AssertPayloadQuad(
            Mesh mesh,
            Sprite sprite,
            Color32 expectedTint,
            Bounds expectedBounds,
            Vector3[] expectedVertices)
        {
            Assert.NotNull(mesh, "Expected generated mesh data.");
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(6, mesh.triangles.Length);
            AssertVectorApproximately(expectedBounds.center, mesh.bounds.center, "mesh bounds center");
            AssertVectorApproximately(expectedBounds.size, mesh.bounds.size, "mesh bounds size");
            AssertVectorArrayApproximately(expectedVertices, mesh.vertices, "mesh vertices");

            Vector4 uv = DataUtility.GetOuterUV(sprite);
            AssertVectorArrayApproximately(
                new[]
                {
                    new Vector2(uv.x, uv.y),
                    new Vector2(uv.z, uv.y),
                    new Vector2(uv.z, uv.w),
                    new Vector2(uv.x, uv.w)
                },
                mesh.uv,
                "mesh uvs");

            var colors = mesh.colors32;
            Assert.AreEqual(4, colors.Length, "Expected one tint color per vertex.");
            for (int i = 0; i < colors.Length; i++)
                AssertColorEquals(expectedTint, colors[i], $"mesh color {i}");
        }

        private static void AssertBiomeMaskMesh(
            Mesh mesh,
            Bounds expectedBounds,
            Vector3[] expectedVertices,
            params Color32[] expectedQuadColors)
        {
            Assert.NotNull(mesh, "Expected generated biome-mask mesh data.");
            Assert.AreEqual(expectedVertices.Length, mesh.vertexCount);
            Assert.AreEqual((expectedVertices.Length / 4) * 6, mesh.triangles.Length);
            AssertVectorApproximately(expectedBounds.center, mesh.bounds.center, "biome-mask bounds center");
            AssertVectorApproximately(expectedBounds.size, mesh.bounds.size, "biome-mask bounds size");
            AssertVectorArrayApproximately(expectedVertices, mesh.vertices, "biome-mask vertices");

            var colors = mesh.colors32;
            Assert.AreEqual(expectedQuadColors.Length * 4, colors.Length, "Expected one biome-mask color per vertex.");
            for (int quad = 0; quad < expectedQuadColors.Length; quad++)
            {
                for (int vertex = 0; vertex < 4; vertex++)
                    AssertColorEquals(expectedQuadColors[quad], colors[(quad * 4) + vertex], $"biome-mask color {quad}:{vertex}");
            }
        }

        private static void AssertVectorArrayApproximately(Vector3[] expected, Vector3[] actual, string label)
        {
            Assert.AreEqual(expected.Length, actual.Length, $"Unexpected {label} count.");
            for (int i = 0; i < expected.Length; i++)
                AssertVectorApproximately(expected[i], actual[i], $"{label} {i}");
        }

        private static void AssertVectorArrayApproximately(Vector2[] expected, Vector2[] actual, string label)
        {
            Assert.AreEqual(expected.Length, actual.Length, $"Unexpected {label} count.");
            for (int i = 0; i < expected.Length; i++)
                AssertVectorApproximately(expected[i], actual[i], $"{label} {i}");
        }

        private static void AssertVectorApproximately(Vector3 expected, Vector3 actual, string label)
        {
            const float epsilon = 0.0001f;
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(epsilon), $"{label}.x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(epsilon), $"{label}.y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(epsilon), $"{label}.z");
        }

        private static void AssertVectorApproximately(Vector2 expected, Vector2 actual, string label)
        {
            const float epsilon = 0.0001f;
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(epsilon), $"{label}.x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(epsilon), $"{label}.y");
        }

        private static void AssertBoundsApproximately(Bounds expected, Bounds actual, string label)
        {
            AssertVectorApproximately(expected.center, actual.center, $"{label} center");
            AssertVectorApproximately(expected.size, actual.size, $"{label} size");
        }

        private static void AssertMatrixApproximately(Matrix4x4 expected, Matrix4x4 actual, string label)
        {
            const float epsilon = 0.0001f;
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                    Assert.That(actual[row, col], Is.EqualTo(expected[row, col]).Within(epsilon), $"{label}[{row},{col}]");
            }
        }

        private static void AssertColorEquals(Color32 expected, Color32 actual, string label)
        {
            Assert.AreEqual(expected.r, actual.r, $"{label}.r");
            Assert.AreEqual(expected.g, actual.g, $"{label}.g");
            Assert.AreEqual(expected.b, actual.b, $"{label}.b");
            Assert.AreEqual(expected.a, actual.a, $"{label}.a");
        }

        private static Type GetNestedType(string name)
        {
            Type type = typeof(ProceduralEnvironment).GetNestedType(name, BindingFlags.NonPublic);
            Assert.NotNull(type, $"Missing nested type {name}");
            return type;
        }

        private static object GetPrivateField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {name}");
            return field.GetValue(target);
        }

        private static object GetPrivateProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, $"Missing property {name}");
            return property.GetValue(target);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {name}");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, $"Missing method {name}");
            method.Invoke(target, null);
        }

        private static object InvokePrivateMethodWithArgs(object target, string name, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, $"Missing method {name}");
            return method.Invoke(target, args);
        }

        private static object InvokeInstanceMethod(object target, string name, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method, $"Missing method {name}");
            return method.Invoke(target, args);
        }

        private static object InvokePrivateStaticMethod(string name, params object[] args)
        {
            MethodInfo method = typeof(ProceduralEnvironment).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method, $"Missing static method {name}");
            return method.Invoke(null, args);
        }
    }
}
