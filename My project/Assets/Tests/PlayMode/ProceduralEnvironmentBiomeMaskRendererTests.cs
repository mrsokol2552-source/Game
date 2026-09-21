using System;
using System.Collections;
using System.Reflection;
using Game.Presentation.Pathfinding;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Sprites;
using UnityEngine.Tilemaps;

using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    public partial class ProceduralEnvironmentFarViewPayloadSourceTests
    {
        [Test]
        public void BiomeMaskChunkRendererBuildsMeshFromCachedMaskData()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            try
            {
                gridGo = new GameObject("BiomeMaskRendererGrid");
                var backgroundGrid = gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                var waterColor = new Color32(16, 80, 192, 128);
                var rockColor = new Color32(112, 96, 72, 160);
                env.UseWaterBiome = true;
                env.UseBiomeMaskChunkRenderer = true;
                env.BiomeMaskChunkRendererChunkSize = 2;
                env.BiomeMaskWaterColor = waterColor;
                env.BiomeMaskRockColor = rockColor;
                SetPrivateField(env, "_backgroundGrid", backgroundGrid);
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_backgroundMaskWidth", 2);
                SetPrivateField(env, "_backgroundMaskHeight", 1);
                SetPrivateField(env, "_backgroundWaterMask", new[] { true, false });
                SetPrivateField(env, "_backgroundRockMask", new[] { false, true });
                SetPrivateField(env, "_backgroundLandDistance", new[] { 0, 0 });
                SetPrivateField(env, "_biomeMaskChunkRendererDataVersion", 1);

                InvokePrivateMethod(env, "RefreshBiomeMaskChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_biomeMaskChunkRendererRoot");
                Assert.NotNull(root, "Expected the biome-mask renderer root to be created.");
                var filter = root.GetComponentInChildren<MeshFilter>();
                Assert.NotNull(filter, "Expected a mesh filter generated from cached biome masks.");
                var renderer = filter.GetComponent<MeshRenderer>();
                Assert.NotNull(renderer, "Expected biome-mask chunk mesh renderer.");
                Assert.NotNull(renderer.sharedMaterial, "Expected biome-mask chunk renderer material.");
                Assert.AreEqual("Hidden/ProceduralEnvironment/BiomeMaskColor", renderer.sharedMaterial.shader.name);
                Assert.AreEqual(env.BackgroundSortingOrder + 1, renderer.sortingOrder);
                Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);
                Assert.False(renderer.receiveShadows);
                AssertBiomeMaskMesh(
                    filter.sharedMesh,
                    new Bounds(new Vector3(1f, 0.5f, 0f), new Vector3(2f, 1f, 0f)),
                    new[]
                    {
                        new Vector3(0f, 0f, 0f),
                        new Vector3(1f, 0f, 0f),
                        new Vector3(1f, 1f, 0f),
                        new Vector3(0f, 1f, 0f),
                        new Vector3(1f, 0f, 0f),
                        new Vector3(2f, 0f, 0f),
                        new Vector3(2f, 1f, 0f),
                        new Vector3(1f, 1f, 0f)
                    },
                    waterColor,
                    rockColor);
                Assert.AreEqual(1, (int)GetPrivateField(env, "_biomeMaskChunkRendererChunkCount"));
                Assert.AreEqual(2, (int)GetPrivateField(env, "_biomeMaskChunkRendererQuadCount"));
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseBiomeMaskChunkRendererResources");
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void BiomeMaskChunkRendererBuildsMeshFromStreamMaskData()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            try
            {
                gridGo = new GameObject("BiomeMaskStreamRendererGrid");
                var backgroundGrid = gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "BackgroundBounds", new BoundsInt(1, 0, 0, 1, 1, 1));

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                env.UseWaterBiome = true;
                env.UseBiomeMaskChunkRenderer = true;
                env.BiomeMaskChunkRendererChunkSize = 2;
                var waterColor = new Color32(24, 96, 224, 144);
                env.BiomeMaskWaterColor = waterColor;
                SetPrivateField(env, "_backgroundGrid", backgroundGrid);
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_backgroundMaskWidth", 2);
                SetPrivateField(env, "_backgroundMaskHeight", 1);
                SetPrivateField(env, "_backgroundWaterMask", new[] { false, true });
                SetPrivateField(env, "_backgroundRockMask", new[] { false, false });
                SetPrivateField(env, "_backgroundLandDistance", new[] { 1, 0 });
                SetPrivateField(env, "_streamingActive", true);
                SetPrivateField(env, "_biomeMaskChunkRendererDataVersion", 1);

                InvokePrivateMethod(env, "RefreshBiomeMaskChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_biomeMaskChunkRendererRoot");
                Assert.NotNull(root, "Expected the stream biome-mask renderer root to be created.");
                var filter = root.GetComponentInChildren<MeshFilter>();
                Assert.NotNull(filter, "Expected a mesh filter generated from stream biome masks.");
                AssertBiomeMaskMesh(
                    filter.sharedMesh,
                    new Bounds(new Vector3(1.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)),
                    new[]
                    {
                        new Vector3(1f, 0f, 0f),
                        new Vector3(2f, 0f, 0f),
                        new Vector3(2f, 1f, 0f),
                        new Vector3(1f, 1f, 0f)
                    },
                    waterColor);
                Assert.AreEqual(1, (int)GetPrivateField(env, "_biomeMaskChunkRendererChunkCount"));
                Assert.AreEqual(1, (int)GetPrivateField(env, "_biomeMaskChunkRendererQuadCount"));
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseBiomeMaskChunkRendererResources");
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void BiomeMaskChunkRendererBuildsPartialEdgeChunksFromStreamMaskData()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            try
            {
                const int originX = 4;
                const int originY = 6;
                const int maskWidth = 8;
                const int maskHeight = 10;

                gridGo = new GameObject("BiomeMaskStreamEdgeRendererGrid");
                var backgroundGrid = gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "BackgroundBounds", new BoundsInt(originX, originY, 0, 3, 3, 1));

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                var waterMask = new bool[maskWidth * maskHeight];
                var rockMask = new bool[maskWidth * maskHeight];
                var landDistance = new int[maskWidth * maskHeight];
                for (int row = originY; row < originY + 3; row++)
                {
                    for (int col = originX; col < originX + 3; col++)
                        waterMask[(row * maskWidth) + col] = true;
                }

                var waterColor = new Color32(32, 112, 224, 144);
                env.UseWaterBiome = true;
                env.UseBiomeMaskChunkRenderer = true;
                env.BiomeMaskChunkRendererChunkSize = 2;
                env.BiomeMaskWaterColor = waterColor;
                SetPrivateField(env, "_backgroundGrid", backgroundGrid);
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_backgroundMaskWidth", maskWidth);
                SetPrivateField(env, "_backgroundMaskHeight", maskHeight);
                SetPrivateField(env, "_backgroundWaterMask", waterMask);
                SetPrivateField(env, "_backgroundRockMask", rockMask);
                SetPrivateField(env, "_backgroundLandDistance", landDistance);
                SetPrivateField(env, "_streamingActive", true);
                SetPrivateField(env, "_biomeMaskChunkRendererDataVersion", 1);

                InvokePrivateMethod(env, "RefreshBiomeMaskChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_biomeMaskChunkRendererRoot");
                Assert.NotNull(root, "Expected the stream edge-chunk biome-mask renderer root to be created.");
                Assert.AreEqual(4, root.GetComponentsInChildren<MeshFilter>().Length, "Expected stream biome masks to split into partial right/top edge chunks.");
                Assert.AreEqual(4, (int)GetPrivateField(env, "_biomeMaskChunkRendererChunkCount"));
                Assert.AreEqual(9, (int)GetPrivateField(env, "_biomeMaskChunkRendererQuadCount"));

                AssertBiomeMaskMesh(
                    GetMeshForChildNamePrefix(root, "BiomeMaskChunk 4,6"),
                    CreateCellBounds(4, 6, 2, 2),
                    CreateCellQuadVertices(4, 6, 2, 2),
                    RepeatColor(waterColor, 4));
                AssertBiomeMaskMesh(
                    GetMeshForChildNamePrefix(root, "BiomeMaskChunk 6,6"),
                    CreateCellBounds(6, 6, 1, 2),
                    CreateCellQuadVertices(6, 6, 1, 2),
                    RepeatColor(waterColor, 2));
                AssertBiomeMaskMesh(
                    GetMeshForChildNamePrefix(root, "BiomeMaskChunk 4,8"),
                    CreateCellBounds(4, 8, 2, 1),
                    CreateCellQuadVertices(4, 8, 2, 1),
                    RepeatColor(waterColor, 2));
                AssertBiomeMaskMesh(
                    GetMeshForChildNamePrefix(root, "BiomeMaskChunk 6,8"),
                    CreateCellBounds(6, 8, 1, 1),
                    CreateCellQuadVertices(6, 8, 1, 1),
                    waterColor);
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseBiomeMaskChunkRendererResources");
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void BiomeMaskChunkRendererLifecycleReleasesRootWhenStreamSourceDisappears()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            try
            {
                gridGo = new GameObject("BiomeMaskStreamSourceLifecycleGrid");
                var backgroundGrid = gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "BackgroundBounds", new BoundsInt(0, 0, 0, 1, 1, 1));

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                env.UseWaterBiome = true;
                env.UseBiomeMaskChunkRenderer = true;
                env.BiomeMaskChunkRendererChunkSize = 1;
                SetPrivateField(env, "_backgroundGrid", backgroundGrid);
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_backgroundMaskWidth", 1);
                SetPrivateField(env, "_backgroundMaskHeight", 1);
                SetPrivateField(env, "_backgroundWaterMask", new[] { true });
                SetPrivateField(env, "_backgroundRockMask", new[] { false });
                SetPrivateField(env, "_backgroundLandDistance", new[] { 0 });
                SetPrivateField(env, "_streamingActive", true);
                SetPrivateField(env, "_biomeMaskChunkRendererDataVersion", 1);

                InvokePrivateMethod(env, "UpdateBiomeMaskChunkRendererLifecycle");

                Assert.NotNull(GetPrivateField(env, "_biomeMaskChunkRendererRoot"), "Expected stream biome-mask source to create a renderer root.");
                Assert.AreEqual(1, (int)GetPrivateField(env, "_biomeMaskChunkRendererChunkCount"));
                Assert.AreEqual(1, (int)GetPrivateField(env, "_biomeMaskChunkRendererQuadCount"));

                chunks.Clear();
                InvokePrivateMethod(env, "UpdateBiomeMaskChunkRendererLifecycle");

                Assert.Null(GetPrivateField(env, "_biomeMaskChunkRendererRoot"), "Expected missing stream biome-mask source to clear the renderer root.");
                Assert.AreEqual(0, (int)GetPrivateField(env, "_biomeMaskChunkRendererChunkCount"));
                Assert.AreEqual(0, (int)GetPrivateField(env, "_biomeMaskChunkRendererQuadCount"));
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseBiomeMaskChunkRendererResources");
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }
    }
}
