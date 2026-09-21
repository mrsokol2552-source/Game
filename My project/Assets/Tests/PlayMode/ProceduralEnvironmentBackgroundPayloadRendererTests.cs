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
        public void BackgroundPayloadChunkRendererBuildsSpriteMeshFromCachedPayload()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("BackgroundPayloadRendererGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels32(new[]
                {
                    new Color32(32, 96, 160, 255),
                    new Color32(32, 96, 160, 255),
                    new Color32(32, 96, 160, 255),
                    new Color32(32, 96, 160, 255)
                });
                texture.Apply(false, false);
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(64, 128, 192, 128);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;

                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 1);
                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, tile, Matrix4x4.identity), 0);

                env.UseBackgroundTilemap = true;
                env.UseBackgroundPayloadChunkRenderer = true;
                env.BackgroundPayloadChunkRendererChunkSize = 1;
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_hasCachedBackgroundFarViewPayload", true);
                SetPrivateField(env, "_cachedBackgroundFarViewPayload", payload);
                SetPrivateField(env, "_cachedBackgroundRenderWidth", 1);
                SetPrivateField(env, "_cachedBackgroundRenderHeight", 1);
                SetPrivateField(env, "_cachedBackgroundFarViewPayloadVersion", 1);

                InvokePrivateMethod(env, "RefreshBackgroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_backgroundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the background payload renderer root to be created.");
                var filter = root.GetComponentInChildren<MeshFilter>();
                Assert.NotNull(filter, "Expected a mesh filter generated from cached background payload.");
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
                Assert.Greater((int)GetPrivateField(env, "_backgroundPayloadChunkRendererQuadCount"), 0);
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseBackgroundPayloadChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void BackgroundPayloadChunkRendererBuildsSpriteMeshFromStreamPayload()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("BackgroundPayloadStreamRendererGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels32(new[]
                {
                    new Color32(96, 160, 64, 255),
                    new Color32(96, 160, 64, 255),
                    new Color32(96, 160, 64, 255),
                    new Color32(96, 160, 64, 255)
                });
                texture.Apply(false, false);
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(96, 160, 64, 192);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;

                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 1);
                payload.SetValue(CreateBackgroundRenderCellDecision(0, 2, 0, tile, Matrix4x4.identity), 0);

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "BackgroundBounds", new BoundsInt(2, 0, 0, 1, 1, 1));
                SetPrivateField(state, "BackgroundPayload", payload);
                SetPrivateField(state, "BackgroundCellCount", 1);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                env.UseBackgroundTilemap = true;
                env.UseBackgroundPayloadChunkRenderer = true;
                env.BackgroundPayloadChunkRendererChunkSize = 1;
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_streamingActive", true);
                SetPrivateField(env, "_cachedBackgroundFarViewPayloadVersion", 1);

                InvokePrivateMethod(env, "RefreshBackgroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_backgroundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the stream background payload renderer root to be created.");
                var filter = root.GetComponentInChildren<MeshFilter>();
                Assert.NotNull(filter, "Expected a mesh filter generated from stream background payload.");
                AssertPayloadQuad(
                    filter.sharedMesh,
                    sprite,
                    expectedTint,
                    new Bounds(new Vector3(2.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)),
                    new[]
                    {
                        new Vector3(2f, 0f, 0f),
                        new Vector3(3f, 0f, 0f),
                        new Vector3(3f, 1f, 0f),
                        new Vector3(2f, 1f, 0f)
                    });
                Assert.Greater((int)GetPrivateField(env, "_backgroundPayloadChunkRendererQuadCount"), 0);
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseBackgroundPayloadChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void BackgroundPayloadChunkRendererAppliesPayloadTransform()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("BackgroundPayloadRendererTransformGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels32(new[]
                {
                    new Color32(24, 48, 96, 255),
                    new Color32(24, 48, 96, 255),
                    new Color32(24, 48, 96, 255),
                    new Color32(24, 48, 96, 255)
                });
                texture.Apply(false, false);
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(192, 96, 48, 224);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;

                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 1);
                var rotation = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));
                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, tile, rotation), 0);

                env.UseBackgroundTilemap = true;
                env.UseBackgroundPayloadChunkRenderer = true;
                env.BackgroundPayloadChunkRendererChunkSize = 1;
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_hasCachedBackgroundFarViewPayload", true);
                SetPrivateField(env, "_cachedBackgroundFarViewPayload", payload);
                SetPrivateField(env, "_cachedBackgroundRenderWidth", 1);
                SetPrivateField(env, "_cachedBackgroundRenderHeight", 1);
                SetPrivateField(env, "_cachedBackgroundFarViewPayloadVersion", 1);

                InvokePrivateMethod(env, "RefreshBackgroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_backgroundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the transformed background payload renderer root to be created.");
                var filter = root.GetComponentInChildren<MeshFilter>();
                Assert.NotNull(filter, "Expected a mesh filter generated from transformed background payload.");
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
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseBackgroundPayloadChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void BackgroundPayloadChunkRendererSplitsCachedPayloadByChunkSize()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("BackgroundPayloadRendererChunkGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels32(new[]
                {
                    new Color32(160, 96, 32, 255),
                    new Color32(160, 96, 32, 255),
                    new Color32(160, 96, 32, 255),
                    new Color32(160, 96, 32, 255)
                });
                texture.Apply(false, false);
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(192, 96, 48, 224);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;

                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 2);
                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, tile, Matrix4x4.identity), 0);
                payload.SetValue(CreateBackgroundRenderCellDecision(1, 1, 0, tile, Matrix4x4.identity), 1);

                env.UseBackgroundTilemap = true;
                env.UseBackgroundPayloadChunkRenderer = true;
                env.BackgroundPayloadChunkRendererChunkSize = 1;
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_hasCachedBackgroundFarViewPayload", true);
                SetPrivateField(env, "_cachedBackgroundFarViewPayload", payload);
                SetPrivateField(env, "_cachedBackgroundRenderWidth", 2);
                SetPrivateField(env, "_cachedBackgroundRenderHeight", 1);
                SetPrivateField(env, "_cachedBackgroundFarViewPayloadVersion", 1);

                InvokePrivateMethod(env, "RefreshBackgroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_backgroundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the chunked background payload renderer root to be created.");
                var filters = root.GetComponentsInChildren<MeshFilter>();
                Assert.AreEqual(2, filters.Length, "Expected one mesh object per cached chunk when chunk size is one.");
                Assert.AreEqual(2, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererChunkCount"));
                Assert.AreEqual(2, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(2, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererTextureCount"));
                for (int i = 0; i < filters.Length; i++)
                {
                    Assert.NotNull(filters[i].sharedMesh, $"Expected generated mesh data for background chunk {i}.");
                    Assert.AreEqual(4, filters[i].sharedMesh.vertexCount);
                    Assert.AreEqual(6, filters[i].sharedMesh.triangles.Length);
                }
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseBackgroundPayloadChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void BackgroundPayloadChunkRendererSeparatesOneChunkByTexture()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tileA = null;
            Tile tileB = null;
            Sprite spriteA = null;
            Sprite spriteB = null;
            Texture2D textureA = null;
            Texture2D textureB = null;
            try
            {
                gridGo = new GameObject("BackgroundPayloadRendererMultiTextureGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                textureA = CreateSolidTexture(new Color32(48, 96, 160, 255));
                textureA.name = "BackgroundPayloadTextureA";
                textureB = CreateSolidTexture(new Color32(160, 112, 48, 255));
                textureB.name = "BackgroundPayloadTextureB";
                spriteA = Sprite.Create(textureA, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                spriteB = Sprite.Create(textureB, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var tintA = new Color32(80, 128, 192, 224);
                var tintB = new Color32(208, 144, 80, 192);
                tileA = ScriptableObject.CreateInstance<Tile>();
                tileA.sprite = spriteA;
                tileA.color = tintA;
                tileB = ScriptableObject.CreateInstance<Tile>();
                tileB.sprite = spriteB;
                tileB.color = tintB;

                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 2);
                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, tileA, Matrix4x4.identity), 0);
                payload.SetValue(CreateBackgroundRenderCellDecision(1, 1, 0, tileB, Matrix4x4.identity), 1);

                env.UseBackgroundTilemap = true;
                env.UseBackgroundPayloadChunkRenderer = true;
                env.BackgroundPayloadChunkRendererChunkSize = 2;
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_hasCachedBackgroundFarViewPayload", true);
                SetPrivateField(env, "_cachedBackgroundFarViewPayload", payload);
                SetPrivateField(env, "_cachedBackgroundRenderWidth", 2);
                SetPrivateField(env, "_cachedBackgroundRenderHeight", 1);
                SetPrivateField(env, "_cachedBackgroundFarViewPayloadVersion", 1);

                InvokePrivateMethod(env, "RefreshBackgroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_backgroundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the multi-texture background payload renderer root to be created.");
                Assert.AreEqual(2, root.GetComponentsInChildren<MeshRenderer>().Length, "Expected one renderer per source texture inside the chunk.");
                Assert.AreEqual(2, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererChunkCount"));
                Assert.AreEqual(2, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(2, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererTextureCount"));

                AssertPayloadQuad(
                    GetMeshForRendererTexture(root, textureA, out _),
                    spriteA,
                    tintA,
                    new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)),
                    new[]
                    {
                        new Vector3(0f, 0f, 0f),
                        new Vector3(1f, 0f, 0f),
                        new Vector3(1f, 1f, 0f),
                        new Vector3(0f, 1f, 0f)
                    });
                AssertPayloadQuad(
                    GetMeshForRendererTexture(root, textureB, out _),
                    spriteB,
                    tintB,
                    new Bounds(new Vector3(1.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)),
                    new[]
                    {
                        new Vector3(1f, 0f, 0f),
                        new Vector3(2f, 0f, 0f),
                        new Vector3(2f, 1f, 0f),
                        new Vector3(1f, 1f, 0f)
                    });
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseBackgroundPayloadChunkRendererResources");
                if (tileA != null) Object.DestroyImmediate(tileA);
                if (tileB != null) Object.DestroyImmediate(tileB);
                if (spriteA != null) Object.DestroyImmediate(spriteA);
                if (spriteB != null) Object.DestroyImmediate(spriteB);
                if (textureA != null) Object.DestroyImmediate(textureA);
                if (textureB != null) Object.DestroyImmediate(textureB);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void BackgroundPayloadChunkRendererBuildsPartialEdgeChunksForOddBounds()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("BackgroundPayloadRendererEdgeChunkGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = CreateSolidTexture(new Color32(96, 136, 184, 255));
                texture.name = "BackgroundEdgeTexture";
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(112, 152, 200, 224);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;

                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 9);
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 3; col++)
                    {
                        int index = (row * 3) + col;
                        payload.SetValue(CreateBackgroundRenderCellDecision(index, col, row, tile, Matrix4x4.identity), index);
                    }
                }

                env.UseBackgroundTilemap = true;
                env.UseBackgroundPayloadChunkRenderer = true;
                env.BackgroundPayloadChunkRendererChunkSize = 2;
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_hasCachedBackgroundFarViewPayload", true);
                SetPrivateField(env, "_cachedBackgroundFarViewPayload", payload);
                SetPrivateField(env, "_cachedBackgroundRenderWidth", 3);
                SetPrivateField(env, "_cachedBackgroundRenderHeight", 3);
                SetPrivateField(env, "_cachedBackgroundFarViewPayloadVersion", 1);

                InvokePrivateMethod(env, "RefreshBackgroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_backgroundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the edge-chunk background payload renderer root to be created.");
                Assert.AreEqual(4, root.GetComponentsInChildren<MeshFilter>().Length, "Expected 2x2 chunks with partial right/top edges.");
                Assert.AreEqual(4, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererChunkCount"));
                Assert.AreEqual(9, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(4, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererTextureCount"));

                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "BackgroundPayloadChunk 0,0 "),
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
                    GetMeshForChildNamePrefix(root, "BackgroundPayloadChunk 2,0 "),
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
                    GetMeshForChildNamePrefix(root, "BackgroundPayloadChunk 0,2 "),
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
                    GetMeshForChildNamePrefix(root, "BackgroundPayloadChunk 2,2 "),
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
                InvokePrivateMethod(env, "ReleaseBackgroundPayloadChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void BackgroundPayloadChunkRendererBuildsPartialEdgeChunksFromStreamPayload()
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

                gridGo = new GameObject("BackgroundPayloadRendererStreamEdgeChunkGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = CreateSolidTexture(new Color32(128, 104, 184, 255));
                texture.name = "BackgroundStreamEdgeTexture";
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(152, 128, 208, 224);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;

                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 9);
                for (int localRow = 0; localRow < 3; localRow++)
                {
                    for (int localCol = 0; localCol < 3; localCol++)
                    {
                        int index = (localRow * 3) + localCol;
                        payload.SetValue(
                            CreateBackgroundRenderCellDecision(index, originX + localCol, originY + localRow, tile, Matrix4x4.identity),
                            index);
                    }
                }

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "BackgroundBounds", new BoundsInt(originX, originY, 0, 3, 3, 1));
                SetPrivateField(state, "BackgroundPayload", payload);
                SetPrivateField(state, "BackgroundCellCount", 9);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                env.UseBackgroundTilemap = true;
                env.UseBackgroundPayloadChunkRenderer = true;
                env.BackgroundPayloadChunkRendererChunkSize = 2;
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_streamingActive", true);
                SetPrivateField(env, "_cachedBackgroundFarViewPayloadVersion", 1);

                InvokePrivateMethod(env, "RefreshBackgroundPayloadChunkRenderer");

                var root = (GameObject)GetPrivateField(env, "_backgroundPayloadChunkRendererRoot");
                Assert.NotNull(root, "Expected the stream edge-chunk background renderer root to be created.");
                Assert.AreEqual(4, root.GetComponentsInChildren<MeshFilter>().Length, "Expected stream background to split into partial right/top edge chunks.");
                Assert.AreEqual(4, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererChunkCount"));
                Assert.AreEqual(9, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(4, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererTextureCount"));

                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "BackgroundPayloadChunk 4,6 "),
                    CreateCellBounds(4, 6, 2, 2),
                    expectedTint,
                    CreateCellQuadVertices(4, 6, 2, 2));
                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "BackgroundPayloadChunk 6,6 "),
                    CreateCellBounds(6, 6, 1, 2),
                    expectedTint,
                    CreateCellQuadVertices(6, 6, 1, 2));
                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "BackgroundPayloadChunk 4,8 "),
                    CreateCellBounds(4, 8, 2, 1),
                    expectedTint,
                    CreateCellQuadVertices(4, 8, 2, 1));
                AssertPayloadMesh(
                    GetMeshForChildNamePrefix(root, "BackgroundPayloadChunk 6,8 "),
                    CreateCellBounds(6, 8, 1, 1),
                    expectedTint,
                    CreateCellQuadVertices(6, 8, 1, 1));
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseBackgroundPayloadChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }
    }
}
