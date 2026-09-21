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
        [Test]
        public void GroundRenderPayloadBoundsUseOnlyGeneratedStreamTiles()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            try
            {
                gridGo = new GameObject("GroundPayloadStreamBoundsGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Ground");
                mapGo.transform.SetParent(gridGo.transform, false);
                var ground = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                tile = ScriptableObject.CreateInstance<Tile>();

                Array generatedCells = CreateNestedArray("TilemapChunkCellData", 3);
                generatedCells.SetValue(InvokePrivateStaticMethod("BuildTilemapChunkCellData", ground, 2, 3, tile, Matrix4x4.identity), 0);
                generatedCells.SetValue(InvokePrivateStaticMethod("BuildTilemapChunkCellData", ground, 4, 3, null, Matrix4x4.identity), 1);
                generatedCells.SetValue(InvokePrivateStaticMethod("BuildTilemapChunkCellData", ground, 6, 3, tile, Matrix4x4.identity), 2);

                Array ignoredCells = CreateNestedArray("TilemapChunkCellData", 1);
                ignoredCells.SetValue(InvokePrivateStaticMethod("BuildTilemapChunkCellData", ground, -10, -10, tile, Matrix4x4.identity), 0);

                Type stateType = GetNestedType("StreamChunkState");
                object generatedState = Activator.CreateInstance(stateType);
                SetPrivateField(generatedState, "Generated", true);
                SetPrivateField(generatedState, "GroundPayloadBounds", new BoundsInt(2, 3, 0, 5, 1, 1));
                SetPrivateField(generatedState, "GroundCells", generatedCells);
                SetPrivateField(generatedState, "GroundCellCount", 2);

                object ignoredState = Activator.CreateInstance(stateType);
                SetPrivateField(ignoredState, "Generated", false);
                SetPrivateField(ignoredState, "GroundPayloadBounds", new BoundsInt(-10, -10, 0, 1, 1, 1));
                SetPrivateField(ignoredState, "GroundCells", ignoredCells);
                SetPrivateField(ignoredState, "GroundCellCount", 1);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, generatedState);
                chunks.Add(new Vector2Int(1, 0), ignoredState);

                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_streamingActive", true);

                bool built = TryGetGroundRenderPayloadBounds(env, includeGround: true, includeTransitions: false, out var bounds);

                Assert.True(built, "Expected stream ground payload bounds from generated cells.");
                AssertBoundsApproximately(
                    new Bounds(new Vector3(4.5f, 3.5f, 0f), new Vector3(5f, 1f, 0f)),
                    bounds,
                    "stream ground bounds");
                Assert.False(
                    TryGetGroundRenderPayloadBounds(env, includeGround: false, includeTransitions: false, out _),
                    "Ground bounds should stay disabled when ground is excluded.");
            }
            finally
            {
                if (tile != null) Object.DestroyImmediate(tile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void GroundPayloadChunkRendererBuildsSpriteMeshFromCachedPayload()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("GroundPayloadRendererGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Ground");
                mapGo.transform.SetParent(gridGo.transform, false);
                var ground = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels32(new[]
                {
                    new Color32(255, 0, 0, 255),
                    new Color32(0, 255, 0, 255),
                    new Color32(0, 0, 255, 255),
                    new Color32(255, 255, 255, 255)
                });
                texture.Apply(false, false);
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(192, 96, 48, 224);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;

                Array payload = CreateNestedArray("GroundRenderCellDecision", 1);
                object decision = Activator.CreateInstance(GetNestedType("GroundRenderCellDecision"));
                SetPrivateField(decision, "GlobalIndex", 0);
                SetPrivateField(decision, "Col", 0);
                SetPrivateField(decision, "Row", 0);
                SetPrivateField(decision, "LayerIndex", 0);
                SetPrivateField(decision, "GroundTile", tile);
                payload.SetValue(decision, 0);

                env.UseGroundPayloadChunkRenderer = true;
                env.GroundPayloadChunkRendererChunkSize = 1;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", payload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 1);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_cachedGroundFarViewPayloadVersion", 1);

                InvokePrivateMethod(env, "RefreshGroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_groundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the payload renderer root to be created.");
                var filter = root.GetComponentInChildren<MeshFilter>();
                Assert.NotNull(filter, "Expected a mesh filter generated from cached payload.");
                AssertPayloadQuad(
                    filter.sharedMesh,
                    sprite,
                    expectedTint,
                    new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)),
                    new[]
                    {
                        new Vector3(0f, 0f, 0f),
                        new Vector3(1f, 0f, 0f),
                        new Vector3(1f, 1f, 0f),
                        new Vector3(0f, 1f, 0f)
                    });
                Assert.Greater((int)GetPrivateField(env, "_groundPayloadChunkRendererQuadCount"), 0);
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseGroundPayloadChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void GroundPayloadChunkRendererSeparatesGroundAndTransitionTextures()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile groundTile = null;
            Tile transitionTile = null;
            Sprite groundSprite = null;
            Sprite transitionSprite = null;
            Texture2D groundTexture = null;
            Texture2D transitionTexture = null;
            try
            {
                gridGo = new GameObject("GroundPayloadRendererMultiTextureGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Ground");
                mapGo.transform.SetParent(gridGo.transform, false);
                var ground = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                groundTexture = CreateSolidTexture(new Color32(64, 144, 96, 255));
                groundTexture.name = "GroundPayloadTexture";
                transitionTexture = CreateSolidTexture(new Color32(120, 88, 176, 255));
                transitionTexture.name = "TransitionPayloadTexture";
                groundSprite = Sprite.Create(groundTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                transitionSprite = Sprite.Create(transitionTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var groundTint = new Color32(96, 176, 128, 224);
                var transitionTint = new Color32(160, 112, 208, 192);
                groundTile = ScriptableObject.CreateInstance<Tile>();
                groundTile.sprite = groundSprite;
                groundTile.color = groundTint;
                transitionTile = ScriptableObject.CreateInstance<Tile>();
                transitionTile.sprite = transitionSprite;
                transitionTile.color = transitionTint;

                Array payload = CreateNestedArray("GroundRenderCellDecision", 1);
                payload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, groundTile, transitionTile), 0);

                env.UseGroundPayloadChunkRenderer = true;
                env.GroundPayloadChunkRendererChunkSize = 1;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", payload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 1);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_cachedGroundFarViewPayloadVersion", 1);

                InvokePrivateMethod(env, "RefreshGroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_groundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the multi-texture ground payload renderer root to be created.");
                Assert.AreEqual(2, root.GetComponentsInChildren<MeshRenderer>().Length, "Expected separate ground and transition renderers.");
                Assert.AreEqual(2, (int)GetPrivateField(env, "_groundPayloadChunkRendererChunkCount"));
                Assert.AreEqual(2, (int)GetPrivateField(env, "_groundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(2, (int)GetPrivateField(env, "_groundPayloadChunkRendererTextureCount"));

                Mesh groundMesh = GetMeshForRendererTexture(root, groundTexture, out var groundRenderer);
                Assert.AreEqual(env.GroundSortingOrder, groundRenderer.sortingOrder);
                AssertPayloadQuad(
                    groundMesh,
                    groundSprite,
                    groundTint,
                    new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)),
                    new[]
                    {
                        new Vector3(0f, 0f, 0f),
                        new Vector3(1f, 0f, 0f),
                        new Vector3(1f, 1f, 0f),
                        new Vector3(0f, 1f, 0f)
                    });

                Mesh transitionMesh = GetMeshForRendererTexture(root, transitionTexture, out var transitionRenderer);
                Assert.AreEqual(env.TransitionSortingOrder, transitionRenderer.sortingOrder);
                AssertPayloadQuad(
                    transitionMesh,
                    transitionSprite,
                    transitionTint,
                    new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)),
                    new[]
                    {
                        new Vector3(0f, 0f, 0f),
                        new Vector3(1f, 0f, 0f),
                        new Vector3(1f, 1f, 0f),
                        new Vector3(0f, 1f, 0f)
                    });
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseGroundPayloadChunkRendererResources");
                if (groundTile != null) Object.DestroyImmediate(groundTile);
                if (transitionTile != null) Object.DestroyImmediate(transitionTile);
                if (groundSprite != null) Object.DestroyImmediate(groundSprite);
                if (transitionSprite != null) Object.DestroyImmediate(transitionSprite);
                if (groundTexture != null) Object.DestroyImmediate(groundTexture);
                if (transitionTexture != null) Object.DestroyImmediate(transitionTexture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void GroundPayloadChunkRendererBuildsPartialEdgeChunksForOddBounds()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("GroundPayloadRendererEdgeChunkGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Ground");
                mapGo.transform.SetParent(gridGo.transform, false);
                var ground = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = CreateSolidTexture(new Color32(80, 168, 112, 255));
                texture.name = "GroundEdgeTexture";
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(104, 192, 136, 224);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;

                Array payload = CreateNestedArray("GroundRenderCellDecision", 9);
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 3; col++)
                    {
                        int index = (row * 3) + col;
                        payload.SetValue(CreateGroundRenderCellDecision(index, col, row, tile, null), index);
                    }
                }

                env.UseGroundPayloadChunkRenderer = true;
                env.GroundPayloadChunkRendererChunkSize = 2;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", payload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 3);
                SetPrivateField(env, "_cachedGroundRenderHeight", 3);
                SetPrivateField(env, "_cachedGroundFarViewPayloadVersion", 1);

                InvokePrivateMethod(env, "RefreshGroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_groundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the edge-chunk ground payload renderer root to be created.");
                Assert.AreEqual(4, root.GetComponentsInChildren<MeshFilter>().Length, "Expected 2x2 chunks with partial right/top edges.");
                Assert.AreEqual(4, (int)GetPrivateField(env, "_groundPayloadChunkRendererChunkCount"));
                Assert.AreEqual(9, (int)GetPrivateField(env, "_groundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(4, (int)GetPrivateField(env, "_groundPayloadChunkRendererTextureCount"));

                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "GroundPayloadChunk Ground 0,0 "),
                    new Bounds(new Vector3(1f, 1f, 0f), new Vector3(2f, 2f, 0f)),
                    expectedTint,
                    new[]
                    {
                        new Vector3(0f, 0f, 0f),
                        new Vector3(1f, 0f, 0f),
                        new Vector3(1f, 1f, 0f),
                        new Vector3(0f, 1f, 0f),
                        new Vector3(1f, 0f, 0f),
                        new Vector3(2f, 0f, 0f),
                        new Vector3(2f, 1f, 0f),
                        new Vector3(1f, 1f, 0f),
                        new Vector3(0f, 1f, 0f),
                        new Vector3(1f, 1f, 0f),
                        new Vector3(1f, 2f, 0f),
                        new Vector3(0f, 2f, 0f),
                        new Vector3(1f, 1f, 0f),
                        new Vector3(2f, 1f, 0f),
                        new Vector3(2f, 2f, 0f),
                        new Vector3(1f, 2f, 0f)
                    });
                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "GroundPayloadChunk Ground 2,0 "),
                    new Bounds(new Vector3(2.5f, 1f, 0f), new Vector3(1f, 2f, 0f)),
                    expectedTint,
                    new[]
                    {
                        new Vector3(2f, 0f, 0f),
                        new Vector3(3f, 0f, 0f),
                        new Vector3(3f, 1f, 0f),
                        new Vector3(2f, 1f, 0f),
                        new Vector3(2f, 1f, 0f),
                        new Vector3(3f, 1f, 0f),
                        new Vector3(3f, 2f, 0f),
                        new Vector3(2f, 2f, 0f)
                    });
                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "GroundPayloadChunk Ground 0,2 "),
                    new Bounds(new Vector3(1f, 2.5f, 0f), new Vector3(2f, 1f, 0f)),
                    expectedTint,
                    new[]
                    {
                        new Vector3(0f, 2f, 0f),
                        new Vector3(1f, 2f, 0f),
                        new Vector3(1f, 3f, 0f),
                        new Vector3(0f, 3f, 0f),
                        new Vector3(1f, 2f, 0f),
                        new Vector3(2f, 2f, 0f),
                        new Vector3(2f, 3f, 0f),
                        new Vector3(1f, 3f, 0f)
                    });
                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "GroundPayloadChunk Ground 2,2 "),
                    new Bounds(new Vector3(2.5f, 2.5f, 0f), new Vector3(1f, 1f, 0f)),
                    expectedTint,
                    new[]
                    {
                        new Vector3(2f, 2f, 0f),
                        new Vector3(3f, 2f, 0f),
                        new Vector3(3f, 3f, 0f),
                        new Vector3(2f, 3f, 0f)
                    });
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseGroundPayloadChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void GroundPayloadChunkRendererBuildsSpriteMeshFromStreamPayload()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("GroundPayloadStreamRendererGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Ground");
                mapGo.transform.SetParent(gridGo.transform, false);
                var ground = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels32(new[]
                {
                    new Color32(255, 128, 0, 255),
                    new Color32(255, 128, 0, 255),
                    new Color32(255, 128, 0, 255),
                    new Color32(255, 128, 0, 255)
                });
                texture.Apply(false, false);
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(255, 128, 64, 192);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;

                Array cells = CreateNestedArray("TilemapChunkCellData", 1);
                var rotation = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));
                object cell = InvokePrivateStaticMethod(
                    "BuildTilemapChunkCellData",
                    ground,
                    0,
                    0,
                    tile,
                    rotation);
                cells.SetValue(cell, 0);

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "GroundPayloadBounds", new BoundsInt(0, 0, 0, 1, 1, 1));
                SetPrivateField(state, "GroundCells", cells);
                SetPrivateField(state, "GroundCellCount", 1);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                env.UseGroundPayloadChunkRenderer = true;
                env.GroundPayloadChunkRendererChunkSize = 1;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_streamingActive", true);
                SetPrivateField(env, "_groundPayloadChunkRendererStreamVersion", 1);

                InvokePrivateMethod(env, "RefreshGroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_groundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the stream payload renderer root to be created.");
                var filter = root.GetComponentInChildren<MeshFilter>();
                Assert.NotNull(filter, "Expected a mesh filter generated from stream payload.");
                AssertPayloadQuad(
                    filter.sharedMesh,
                    sprite,
                    expectedTint,
                    new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)),
                    new[]
                    {
                        new Vector3(1f, 0f, 0f),
                        new Vector3(1f, 1f, 0f),
                        new Vector3(0f, 1f, 0f),
                        new Vector3(0f, 0f, 0f)
                    });
                Assert.Greater((int)GetPrivateField(env, "_groundPayloadChunkRendererQuadCount"), 0);
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseGroundPayloadChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void GroundPayloadChunkRendererBuildsPartialEdgeChunksFromStreamPayload()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                const int originX = 4;
                const int originY = 6;

                gridGo = new GameObject("GroundPayloadRendererStreamEdgeChunkGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Ground");
                mapGo.transform.SetParent(gridGo.transform, false);
                var ground = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = CreateSolidTexture(new Color32(72, 184, 136, 255));
                texture.name = "GroundStreamEdgeTexture";
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(96, 208, 160, 224);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;

                Array cells = CreateNestedArray("TilemapChunkCellData", 9);
                for (int localRow = 0; localRow < 3; localRow++)
                {
                    for (int localCol = 0; localCol < 3; localCol++)
                    {
                        int index = (localRow * 3) + localCol;
                        cells.SetValue(
                            InvokePrivateStaticMethod(
                                "BuildTilemapChunkCellData",
                                ground,
                                originX + localCol,
                                originY + localRow,
                                tile,
                                Matrix4x4.identity),
                            index);
                    }
                }

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "GroundPayloadBounds", new BoundsInt(originX, originY, 0, 3, 3, 1));
                SetPrivateField(state, "GroundCells", cells);
                SetPrivateField(state, "GroundCellCount", 9);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                env.UseGroundPayloadChunkRenderer = true;
                env.GroundPayloadChunkRendererChunkSize = 2;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_streamingActive", true);
                SetPrivateField(env, "_groundPayloadChunkRendererStreamVersion", 1);

                InvokePrivateMethod(env, "RefreshGroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_groundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the stream edge-chunk ground renderer root to be created.");
                Assert.AreEqual(4, root.GetComponentsInChildren<MeshFilter>().Length, "Expected stream ground to split into partial right/top edge chunks.");
                Assert.AreEqual(4, (int)GetPrivateField(env, "_groundPayloadChunkRendererChunkCount"));
                Assert.AreEqual(9, (int)GetPrivateField(env, "_groundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(4, (int)GetPrivateField(env, "_groundPayloadChunkRendererTextureCount"));

                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "GroundPayloadChunk StreamGround 4,6 "),
                    CreateCellBounds(4, 6, 2, 2),
                    expectedTint,
                    CreateCellQuadVertices(4, 6, 2, 2));
                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "GroundPayloadChunk StreamGround 6,6 "),
                    CreateCellBounds(6, 6, 1, 2),
                    expectedTint,
                    CreateCellQuadVertices(6, 6, 1, 2));
                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "GroundPayloadChunk StreamGround 4,8 "),
                    CreateCellBounds(4, 8, 2, 1),
                    expectedTint,
                    CreateCellQuadVertices(4, 8, 2, 1));
                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "GroundPayloadChunk StreamGround 6,8 "),
                    CreateCellBounds(6, 8, 1, 1),
                    expectedTint,
                    CreateCellQuadVertices(6, 8, 1, 1));
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseGroundPayloadChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void GroundPayloadChunkRendererLifecycleRebuildsMissingRootForCurrentPayloadVersion()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("GroundPayloadLifecycleGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Ground");
                mapGo.transform.SetParent(gridGo.transform, false);
                var ground = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = CreateSolidTexture(new Color32(32, 144, 96, 255));
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = new Color32(64, 176, 112, 224);

                Array payload = CreateNestedArray("GroundRenderCellDecision", 1);
                payload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, tile, null), 0);

                env.UseGroundPayloadChunkRenderer = true;
                env.GroundPayloadChunkRendererChunkSize = 1;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", payload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 1);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_cachedGroundFarViewPayloadVersion", 1);

                InvokePrivateMethod(env, "UpdateGroundPayloadChunkRendererLifecycle");

                var initialRoot = (GameObject)GetPrivateField(env, "_groundPayloadChunkRendererRoot");
                Assert.NotNull(initialRoot, "Expected lifecycle to create the ground payload renderer root.");
                Assert.AreEqual(1, (int)GetPrivateField(env, "_groundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(1, (int)GetPrivateField(env, "_groundPayloadChunkRendererVersion"));

                InvokePrivateStaticMethod("ReleaseGroundPayloadChunkRendererMeshes", initialRoot);
                Object.DestroyImmediate(initialRoot);
                SetPrivateField(env, "_groundPayloadChunkRendererRoot", null);

                InvokePrivateMethod(env, "UpdateGroundPayloadChunkRendererLifecycle");

                var rebuiltRoot = (GameObject)GetPrivateField(env, "_groundPayloadChunkRendererRoot");
                Assert.NotNull(rebuiltRoot, "Expected lifecycle to rebuild a missing root even when the payload version is unchanged.");
                Assert.AreEqual(1, (int)GetPrivateField(env, "_groundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(1, (int)GetPrivateField(env, "_groundPayloadChunkRendererVersion"));
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseGroundPayloadChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }
    }
}
