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
        public void FarViewBackgroundChunkSourceSlicesPayloadByChunkBounds()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile0 = null;
            Tile tile1 = null;
            Tile tile2 = null;
            try
            {
                gridGo = new GameObject("FarViewBackgroundChunkSourceGrid");
                var backgroundGrid = gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                tile0 = ScriptableObject.CreateInstance<Tile>();
                tile1 = ScriptableObject.CreateInstance<Tile>();
                tile2 = ScriptableObject.CreateInstance<Tile>();

                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 3);
                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, tile0, Matrix4x4.identity), 0);
                payload.SetValue(CreateBackgroundRenderCellDecision(1, 1, 0, tile1, Matrix4x4.identity), 1);
                payload.SetValue(CreateBackgroundRenderCellDecision(2, 2, 0, tile2, Matrix4x4.identity), 2);

                object bakeSource = Activator.CreateInstance(GetNestedType("FarViewBakeSource"));
                SetPrivateField(bakeSource, "HasBackgroundPayload", true);
                SetPrivateField(bakeSource, "BackgroundPayload", payload);
                SetPrivateField(bakeSource, "BackgroundBounds", new Bounds(new Vector3(1.5f, 0.5f, 0f), new Vector3(3f, 1f, 0f)));
                SetPrivateField(bakeSource, "BackgroundPayloadWidth", 3);
                SetPrivateField(bakeSource, "BackgroundPayloadHeight", 1);
                SetPrivateField(bakeSource, "BackgroundPayloadOrigin", Vector2Int.zero);
                SetPrivateField(bakeSource, "BackgroundPayloadVersion", 7);

                SetPrivateField(env, "_backgroundGrid", backgroundGrid);
                SetPrivateField(env, "_background", background);

                var chunkBounds = new Bounds(new Vector3(1.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                object[] args =
                {
                    bakeSource,
                    null,
                    chunkBounds,
                    Activator.CreateInstance(GetNestedType("FarViewBackgroundChunkSource"))
                };

                bool built = (bool)InvokePrivateMethodWithArgs(env, "TryBuildFarViewBackgroundChunkSource", args);

                Assert.True(built, "Expected far-view background chunk source to slice the payload.");
                object chunkSource = args[3];
                Assert.AreEqual(new RectInt(1, 0, 1, 1), (RectInt)GetPrivateField(chunkSource, "CellRect"));
                Assert.AreEqual(7, (int)GetPrivateField(chunkSource, "PayloadVersion"));
                AssertBoundsApproximately(
                    new Bounds(new Vector3(1.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)),
                    (Bounds)GetPrivateField(chunkSource, "WorldBounds"),
                    "far-view background chunk bounds");

                var decisions = (Array)GetPrivateField(chunkSource, "Decisions");
                Assert.AreEqual(1, decisions.Length);
                object decision = decisions.GetValue(0);
                Assert.AreEqual(1, (int)GetPrivateField(decision, "GlobalIndex"));
                Assert.AreEqual(1, (int)GetPrivateField(decision, "Col"));
                Assert.AreEqual(0, (int)GetPrivateField(decision, "Row"));
                Assert.AreSame(tile1, (TileBase)GetPrivateField(decision, "Tile"));
            }
            finally
            {
                if (tile0 != null) Object.DestroyImmediate(tile0);
                if (tile1 != null) Object.DestroyImmediate(tile1);
                if (tile2 != null) Object.DestroyImmediate(tile2);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void StreamBackgroundFarViewPayloadMergesSparseGeneratedChunksWithOriginAndBounds()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tileA = null;
            Tile tileB = null;
            try
            {
                gridGo = new GameObject("StreamBackgroundFarViewPayloadGrid");
                var backgroundGrid = gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                tileA = ScriptableObject.CreateInstance<Tile>();
                tileB = ScriptableObject.CreateInstance<Tile>();

                Array payloadA = CreateNestedArray("BackgroundRenderCellDecision", 2);
                payloadA.SetValue(CreateBackgroundRenderCellDecision(0, 2, 3, tileA, Matrix4x4.identity), 0);
                payloadA.SetValue(CreateBackgroundRenderCellDecision(1, 3, 3, null, Matrix4x4.identity), 1);
                Array payloadB = CreateNestedArray("BackgroundRenderCellDecision", 1);
                payloadB.SetValue(CreateBackgroundRenderCellDecision(2, 4, 3, tileB, Matrix4x4.identity), 0);

                Type stateType = GetNestedType("StreamChunkState");
                object stateA = Activator.CreateInstance(stateType);
                SetPrivateField(stateA, "Generated", true);
                SetPrivateField(stateA, "BackgroundPayload", payloadA);
                SetPrivateField(stateA, "BackgroundCellCount", 1);
                object stateB = Activator.CreateInstance(stateType);
                SetPrivateField(stateB, "Generated", true);
                SetPrivateField(stateB, "BackgroundPayload", payloadB);
                SetPrivateField(stateB, "BackgroundCellCount", 1);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(new Vector2Int(0, 0), stateA);
                chunks.Add(new Vector2Int(1, 0), stateB);

                env.FarViewIncludeBackground = true;
                env.UseBackgroundTilemap = true;
                SetPrivateField(env, "_streamingActive", true);
                SetPrivateField(env, "_backgroundGrid", backgroundGrid);
                SetPrivateField(env, "_background", background);

                bool built = TryBuildStreamBackgroundFarViewPayload(
                    env,
                    out var streamPayload,
                    out var bounds,
                    out int width,
                    out int height,
                    out var origin);

                Assert.True(built, "Expected stream background far-view payload from generated stream chunks.");
                Assert.AreEqual(3, width);
                Assert.AreEqual(1, height);
                Assert.AreEqual(new Vector2Int(2, 3), origin);
                AssertBoundsApproximately(
                    new Bounds(new Vector3(3.5f, 3.5f, 0f), new Vector3(3f, 1f, 0f)),
                    bounds,
                    "stream background payload bounds");
                Assert.AreEqual(3, streamPayload.Length);
                Assert.AreSame(tileA, (TileBase)GetPrivateField(streamPayload.GetValue(0), "Tile"));
                Assert.Null((TileBase)GetPrivateField(streamPayload.GetValue(1), "Tile"), "Expected sparse gap between stream background chunks.");
                Assert.AreSame(tileB, (TileBase)GetPrivateField(streamPayload.GetValue(2), "Tile"));
            }
            finally
            {
                if (tileA != null) Object.DestroyImmediate(tileA);
                if (tileB != null) Object.DestroyImmediate(tileB);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewBackgroundChunkSourceReusesAppliedSliceForMatchingVersionAndBounds()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile initialTile = null;
            Tile changedTile = null;
            try
            {
                gridGo = new GameObject("FarViewBackgroundChunkSourceReuseGrid");
                var backgroundGrid = gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                initialTile = ScriptableObject.CreateInstance<Tile>();
                changedTile = ScriptableObject.CreateInstance<Tile>();

                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 1);
                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, initialTile, Matrix4x4.identity), 0);

                object bakeSource = Activator.CreateInstance(GetNestedType("FarViewBakeSource"));
                SetPrivateField(bakeSource, "HasBackgroundPayload", true);
                SetPrivateField(bakeSource, "BackgroundPayload", payload);
                SetPrivateField(bakeSource, "BackgroundBounds", new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)));
                SetPrivateField(bakeSource, "BackgroundPayloadWidth", 1);
                SetPrivateField(bakeSource, "BackgroundPayloadHeight", 1);
                SetPrivateField(bakeSource, "BackgroundPayloadOrigin", Vector2Int.zero);
                SetPrivateField(bakeSource, "BackgroundPayloadVersion", 11);

                SetPrivateField(env, "_backgroundGrid", backgroundGrid);
                SetPrivateField(env, "_background", background);

                var chunkBounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewBackgroundChunkSource(env, bakeSource, null, chunkBounds, out var firstSource);
                Assert.True(built, "Expected initial far-view background chunk source.");
                var firstDecisions = (Array)GetPrivateField(firstSource, "Decisions");

                object chunk = CreateFarViewChunk();
                ApplyFarViewBackgroundChunkSource(chunk, firstSource);

                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, changedTile, Matrix4x4.identity), 0);
                bool reused = TryBuildFarViewBackgroundChunkSource(env, bakeSource, chunk, chunkBounds, out var reusedSource);

                Assert.True(reused, "Expected matching far-view background version and bounds to reuse the applied slice.");
                Assert.AreEqual(1, (int)GetPrivateField(env, "_farViewBackgroundSliceCacheHits"));
                Assert.AreEqual(1, (int)GetPrivateField(env, "_farViewBackgroundSliceCacheMisses"));
                Assert.AreSame(firstDecisions, GetPrivateField(reusedSource, "Decisions"));
                var decisions = (Array)GetPrivateField(reusedSource, "Decisions");
                Assert.AreSame(initialTile, (TileBase)GetPrivateField(decisions.GetValue(0), "Tile"));
            }
            finally
            {
                if (initialTile != null) Object.DestroyImmediate(initialTile);
                if (changedTile != null) Object.DestroyImmediate(changedTile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewBackgroundChunkSourceRebuildsAppliedSliceWhenVersionChanges()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile initialTile = null;
            Tile changedTile = null;
            try
            {
                gridGo = new GameObject("FarViewBackgroundChunkSourceVersionGrid");
                var backgroundGrid = gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                initialTile = ScriptableObject.CreateInstance<Tile>();
                changedTile = ScriptableObject.CreateInstance<Tile>();

                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 1);
                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, initialTile, Matrix4x4.identity), 0);

                object bakeSource = Activator.CreateInstance(GetNestedType("FarViewBakeSource"));
                SetPrivateField(bakeSource, "HasBackgroundPayload", true);
                SetPrivateField(bakeSource, "BackgroundPayload", payload);
                SetPrivateField(bakeSource, "BackgroundBounds", new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)));
                SetPrivateField(bakeSource, "BackgroundPayloadWidth", 1);
                SetPrivateField(bakeSource, "BackgroundPayloadHeight", 1);
                SetPrivateField(bakeSource, "BackgroundPayloadOrigin", Vector2Int.zero);
                SetPrivateField(bakeSource, "BackgroundPayloadVersion", 13);

                SetPrivateField(env, "_backgroundGrid", backgroundGrid);
                SetPrivateField(env, "_background", background);

                var chunkBounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewBackgroundChunkSource(env, bakeSource, null, chunkBounds, out var firstSource);
                Assert.True(built, "Expected initial far-view background chunk source.");
                var firstDecisions = (Array)GetPrivateField(firstSource, "Decisions");

                object chunk = CreateFarViewChunk();
                ApplyFarViewBackgroundChunkSource(chunk, firstSource);

                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, changedTile, Matrix4x4.identity), 0);
                SetPrivateField(bakeSource, "BackgroundPayloadVersion", 14);
                bool rebuilt = TryBuildFarViewBackgroundChunkSource(env, bakeSource, chunk, chunkBounds, out var rebuiltSource);

                Assert.True(rebuilt, "Expected changed far-view background payload version to rebuild from payload.");
                Assert.AreEqual(0, (int)GetPrivateField(env, "_farViewBackgroundSliceCacheHits"));
                Assert.AreEqual(2, (int)GetPrivateField(env, "_farViewBackgroundSliceCacheMisses"));
                var rebuiltDecisions = (Array)GetPrivateField(rebuiltSource, "Decisions");
                Assert.AreNotSame(firstDecisions, rebuiltDecisions);
                Assert.AreSame(changedTile, (TileBase)GetPrivateField(rebuiltDecisions.GetValue(0), "Tile"));
            }
            finally
            {
                if (initialTile != null) Object.DestroyImmediate(initialTile);
                if (changedTile != null) Object.DestroyImmediate(changedTile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewBackgroundChunkSourceRebuildsAppliedSliceWhenCaptureBoundsChange()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile firstTile = null;
            Tile secondTile = null;
            try
            {
                gridGo = new GameObject("FarViewBackgroundChunkSourceBoundsGrid");
                var backgroundGrid = gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                firstTile = ScriptableObject.CreateInstance<Tile>();
                secondTile = ScriptableObject.CreateInstance<Tile>();

                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 2);
                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, firstTile, Matrix4x4.identity), 0);
                payload.SetValue(CreateBackgroundRenderCellDecision(1, 1, 0, secondTile, Matrix4x4.identity), 1);

                object bakeSource = Activator.CreateInstance(GetNestedType("FarViewBakeSource"));
                SetPrivateField(bakeSource, "HasBackgroundPayload", true);
                SetPrivateField(bakeSource, "BackgroundPayload", payload);
                SetPrivateField(bakeSource, "BackgroundBounds", new Bounds(new Vector3(1f, 0.5f, 0f), new Vector3(2f, 1f, 0f)));
                SetPrivateField(bakeSource, "BackgroundPayloadWidth", 2);
                SetPrivateField(bakeSource, "BackgroundPayloadHeight", 1);
                SetPrivateField(bakeSource, "BackgroundPayloadOrigin", Vector2Int.zero);
                SetPrivateField(bakeSource, "BackgroundPayloadVersion", 17);

                SetPrivateField(env, "_backgroundGrid", backgroundGrid);
                SetPrivateField(env, "_background", background);

                var firstBounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewBackgroundChunkSource(env, bakeSource, null, firstBounds, out var firstSource);
                Assert.True(built, "Expected initial far-view background chunk source.");
                var firstDecisions = (Array)GetPrivateField(firstSource, "Decisions");

                object chunk = CreateFarViewChunk();
                ApplyFarViewBackgroundChunkSource(chunk, firstSource);

                var secondBounds = new Bounds(new Vector3(1.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool rebuilt = TryBuildFarViewBackgroundChunkSource(env, bakeSource, chunk, secondBounds, out var rebuiltSource);

                Assert.True(rebuilt, "Expected changed far-view background capture bounds to rebuild from payload.");
                Assert.AreEqual(0, (int)GetPrivateField(env, "_farViewBackgroundSliceCacheHits"));
                Assert.AreEqual(2, (int)GetPrivateField(env, "_farViewBackgroundSliceCacheMisses"));
                Assert.AreEqual(new RectInt(1, 0, 1, 1), (RectInt)GetPrivateField(rebuiltSource, "CellRect"));
                var rebuiltDecisions = (Array)GetPrivateField(rebuiltSource, "Decisions");
                Assert.AreNotSame(firstDecisions, rebuiltDecisions);
                Assert.AreSame(secondTile, (TileBase)GetPrivateField(rebuiltDecisions.GetValue(0), "Tile"));
            }
            finally
            {
                if (firstTile != null) Object.DestroyImmediate(firstTile);
                if (secondTile != null) Object.DestroyImmediate(secondTile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewBackgroundChunkSourceDefaultApplyClearsStaleSlice()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            try
            {
                gridGo = new GameObject("FarViewBackgroundChunkSourceClearGrid");
                var backgroundGrid = gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                tile = ScriptableObject.CreateInstance<Tile>();
                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 1);
                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, tile, Matrix4x4.identity), 0);

                object bakeSource = Activator.CreateInstance(GetNestedType("FarViewBakeSource"));
                SetPrivateField(bakeSource, "HasBackgroundPayload", true);
                SetPrivateField(bakeSource, "BackgroundPayload", payload);
                SetPrivateField(bakeSource, "BackgroundBounds", new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)));
                SetPrivateField(bakeSource, "BackgroundPayloadWidth", 1);
                SetPrivateField(bakeSource, "BackgroundPayloadHeight", 1);
                SetPrivateField(bakeSource, "BackgroundPayloadOrigin", Vector2Int.zero);
                SetPrivateField(bakeSource, "BackgroundPayloadVersion", 19);

                SetPrivateField(env, "_backgroundGrid", backgroundGrid);
                SetPrivateField(env, "_background", background);

                var chunkBounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewBackgroundChunkSource(env, bakeSource, null, chunkBounds, out var source);
                Assert.True(built, "Expected initial far-view background chunk source.");

                object chunk = CreateFarViewChunk();
                ApplyFarViewBackgroundChunkSource(chunk, source);

                var outsideBounds = new Bounds(new Vector3(3.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool hasOutsideSource = TryBuildFarViewBackgroundChunkSource(env, bakeSource, chunk, outsideBounds, out _);
                Assert.False(hasOutsideSource, "Expected no background chunk source outside payload bounds.");

                ApplyFarViewBackgroundChunkSource(chunk, Activator.CreateInstance(GetNestedType("FarViewBackgroundChunkSource")));

                object renderSource = GetPrivateField(chunk, "RenderSource");
                Assert.False((bool)GetPrivateProperty(renderSource, "HasBackgroundPayload"));
                Assert.Null(GetPrivateField(renderSource, "BackgroundPayload"));
                Assert.AreEqual(0, (int)GetPrivateField(renderSource, "BackgroundPayloadVersion"));
                Assert.AreEqual(0, (int)GetPrivateField(renderSource, "BackgroundCellCount"));
                Assert.AreEqual(default(RectInt), (RectInt)GetPrivateField(renderSource, "BackgroundCellRect"));
                AssertBoundsApproximately(default, (Bounds)GetPrivateField(renderSource, "BackgroundBounds"), "cleared background bounds");
            }
            finally
            {
                if (tile != null) Object.DestroyImmediate(tile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewTileChunkSourceCombinesCachedGroundAndPlacementPayloads()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile groundTile = null;
            Tile propTile = null;
            Sprite groundSprite = null;
            Sprite propSprite = null;
            Texture2D groundTexture = null;
            Texture2D propTexture = null;
            try
            {
                gridGo = new GameObject("FarViewTileChunkSourceGrid");
                gridGo.AddComponent<Grid>();

                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                groundGo.AddComponent<TilemapRenderer>();

                var propsGo = new GameObject("Props");
                propsGo.transform.SetParent(gridGo.transform, false);
                var props = propsGo.AddComponent<Tilemap>();
                propsGo.AddComponent<TilemapRenderer>();

                groundTexture = CreateSolidTexture(new Color32(64, 128, 96, 255));
                groundSprite = Sprite.Create(groundTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var groundTint = new Color32(96, 160, 112, 224);
                groundTile = ScriptableObject.CreateInstance<Tile>();
                groundTile.sprite = groundSprite;
                groundTile.color = groundTint;
                groundTile.transform = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));

                propTexture = CreateSolidTexture(new Color32(128, 96, 64, 255));
                propSprite = Sprite.Create(propTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var propTint = new Color32(208, 144, 96, 192);
                propTile = ScriptableObject.CreateInstance<Tile>();
                propTile.sprite = propSprite;
                propTile.color = propTint;

                Array groundPayload = CreateNestedArray("GroundRenderCellDecision", 3);
                groundPayload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, null, null), 0);
                groundPayload.SetValue(CreateGroundRenderCellDecision(1, 1, 0, groundTile, null), 1);
                groundPayload.SetValue(CreateGroundRenderCellDecision(2, 2, 0, null, null), 2);

                var placements = (IList)CreateNestedList("Placement");
                placements.Add(CreatePlacement(1, 0, propTile));
                placements.Add(CreatePlacement(2, 0, propTile));

                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = false;
                env.FarViewIncludeProps = true;
                env.FarViewIncludeBlockers = false;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_props", props);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", groundPayload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 3);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_cachedGroundRenderUsesTransition", false);
                InvokePrivateMethodWithArgs(env, "CachePropPlacementRenderPayload", CreateNestedListArray("Placement", placements));

                var chunkBounds = new Bounds(new Vector3(1.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewTileChunkSource(env, chunkBounds, out var source);

                Assert.True(built, "Expected far-view tile chunk source to combine cached ground and placement payloads.");
                Assert.AreEqual(1, (int)GetPrivateProperty(source, "GroundCellCount"));
                Assert.AreEqual(1, (int)GetPrivateProperty(source, "PropCellCount"));
                Assert.Null(GetPrivateField(source, "Transitions"));
                Assert.Null(GetPrivateField(source, "Blockers"));
                AssertTilemapChunkSnapshot(
                    GetPrivateField(source, "Ground"),
                    "Ground",
                    new BoundsInt(1, 0, 0, 1, 1, 1),
                    1,
                    0,
                    groundTile,
                    groundTint,
                    new Vector3(1f, 0f, 0f),
                    new Vector3(2f, 1f, 0f),
                    groundTile.transform);
                AssertTilemapChunkSnapshot(
                    GetPrivateField(source, "Props"),
                    "Props",
                    new BoundsInt(1, 0, 0, 1, 1, 1),
                    1,
                    0,
                    propTile,
                    propTint,
                    new Vector3(1f, 0f, 0f),
                    new Vector3(2f, 1f, 0f),
                    Matrix4x4.identity);
            }
            finally
            {
                if (groundTile != null) Object.DestroyImmediate(groundTile);
                if (propTile != null) Object.DestroyImmediate(propTile);
                if (groundSprite != null) Object.DestroyImmediate(groundSprite);
                if (propSprite != null) Object.DestroyImmediate(propSprite);
                if (groundTexture != null) Object.DestroyImmediate(groundTexture);
                if (propTexture != null) Object.DestroyImmediate(propTexture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewTileChunkSourceReusesAppliedSnapshotForMatchingVersionAndBounds()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile initialTile = null;
            Tile changedTile = null;
            try
            {
                gridGo = new GameObject("FarViewTileChunkSourceReuseGrid");
                gridGo.AddComponent<Grid>();

                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                groundGo.AddComponent<TilemapRenderer>();

                var initialTint = new Color32(80, 144, 96, 255);
                initialTile = ScriptableObject.CreateInstance<Tile>();
                initialTile.color = initialTint;
                changedTile = ScriptableObject.CreateInstance<Tile>();
                changedTile.color = new Color32(192, 64, 64, 255);

                Array groundPayload = CreateNestedArray("GroundRenderCellDecision", 1);
                groundPayload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, initialTile, null), 0);

                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = false;
                env.FarViewIncludeProps = false;
                env.FarViewIncludeBlockers = false;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", groundPayload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 1);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_cachedGroundRenderUsesTransition", false);
                SetPrivateField(env, "_cachedFarViewTileSnapshotVersion", 3);

                var chunkBounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewTileChunkSource(env, chunkBounds, out var firstSource);
                Assert.True(built, "Expected initial far-view tile chunk source.");
                object firstGroundSnapshot = GetPrivateField(firstSource, "Ground");

                object chunk = CreateFarViewChunk();
                ApplyFarViewTileChunkSource(env, chunk, firstSource, chunkBounds);

                groundPayload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, changedTile, null), 0);
                bool reused = TryBuildFarViewTileChunkSource(env, chunk, chunkBounds, out var reusedSource);

                Assert.True(reused, "Expected matching far-view chunk version and bounds to reuse the applied snapshot.");
                Assert.AreEqual(1, (int)GetPrivateField(env, "_farViewTileSliceCacheHits"));
                Assert.AreEqual(1, (int)GetPrivateField(env, "_farViewTileSliceCacheMisses"));
                Assert.AreSame(firstGroundSnapshot, GetPrivateField(reusedSource, "Ground"));
                AssertTilemapChunkSnapshot(
                    GetPrivateField(reusedSource, "Ground"),
                    "Ground",
                    new BoundsInt(0, 0, 0, 1, 1, 1),
                    0,
                    0,
                    initialTile,
                    initialTint,
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 1f, 0f),
                    Matrix4x4.identity);
            }
            finally
            {
                if (initialTile != null) Object.DestroyImmediate(initialTile);
                if (changedTile != null) Object.DestroyImmediate(changedTile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewTileChunkSourceRebuildsAppliedSnapshotWhenVersionChanges()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile initialTile = null;
            Tile changedTile = null;
            try
            {
                gridGo = new GameObject("FarViewTileChunkSourceVersionGrid");
                gridGo.AddComponent<Grid>();

                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                groundGo.AddComponent<TilemapRenderer>();

                initialTile = ScriptableObject.CreateInstance<Tile>();
                initialTile.color = new Color32(64, 128, 96, 255);
                var changedTint = new Color32(200, 96, 56, 255);
                changedTile = ScriptableObject.CreateInstance<Tile>();
                changedTile.color = changedTint;

                Array groundPayload = CreateNestedArray("GroundRenderCellDecision", 1);
                groundPayload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, initialTile, null), 0);

                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = false;
                env.FarViewIncludeProps = false;
                env.FarViewIncludeBlockers = false;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", groundPayload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 1);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_cachedGroundRenderUsesTransition", false);
                SetPrivateField(env, "_cachedFarViewTileSnapshotVersion", 5);

                var chunkBounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewTileChunkSource(env, chunkBounds, out var firstSource);
                Assert.True(built, "Expected initial far-view tile chunk source.");
                object firstGroundSnapshot = GetPrivateField(firstSource, "Ground");

                object chunk = CreateFarViewChunk();
                ApplyFarViewTileChunkSource(env, chunk, firstSource, chunkBounds);

                groundPayload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, changedTile, null), 0);
                SetPrivateField(env, "_cachedFarViewTileSnapshotVersion", 6);
                bool rebuilt = TryBuildFarViewTileChunkSource(env, chunk, chunkBounds, out var rebuiltSource);

                Assert.True(rebuilt, "Expected changed far-view tile snapshot version to rebuild from payload.");
                Assert.AreEqual(0, (int)GetPrivateField(env, "_farViewTileSliceCacheHits"));
                Assert.AreEqual(2, (int)GetPrivateField(env, "_farViewTileSliceCacheMisses"));
                Assert.AreNotSame(firstGroundSnapshot, GetPrivateField(rebuiltSource, "Ground"));
                AssertTilemapChunkSnapshot(
                    GetPrivateField(rebuiltSource, "Ground"),
                    "Ground",
                    new BoundsInt(0, 0, 0, 1, 1, 1),
                    0,
                    0,
                    changedTile,
                    changedTint,
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 1f, 0f),
                    Matrix4x4.identity);
            }
            finally
            {
                if (initialTile != null) Object.DestroyImmediate(initialTile);
                if (changedTile != null) Object.DestroyImmediate(changedTile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewTileChunkSourceRebuildsAppliedSnapshotWhenCaptureBoundsChange()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile firstTile = null;
            Tile secondTile = null;
            try
            {
                gridGo = new GameObject("FarViewTileChunkSourceBoundsGrid");
                gridGo.AddComponent<Grid>();

                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                groundGo.AddComponent<TilemapRenderer>();

                firstTile = ScriptableObject.CreateInstance<Tile>();
                firstTile.color = new Color32(64, 128, 96, 255);
                var secondTint = new Color32(72, 112, 200, 255);
                secondTile = ScriptableObject.CreateInstance<Tile>();
                secondTile.color = secondTint;

                Array groundPayload = CreateNestedArray("GroundRenderCellDecision", 2);
                groundPayload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, firstTile, null), 0);
                groundPayload.SetValue(CreateGroundRenderCellDecision(1, 1, 0, secondTile, null), 1);

                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = false;
                env.FarViewIncludeProps = false;
                env.FarViewIncludeBlockers = false;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", groundPayload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 2);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_cachedGroundRenderUsesTransition", false);
                SetPrivateField(env, "_cachedFarViewTileSnapshotVersion", 8);

                var firstBounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewTileChunkSource(env, firstBounds, out var firstSource);
                Assert.True(built, "Expected initial far-view tile chunk source.");
                object firstGroundSnapshot = GetPrivateField(firstSource, "Ground");

                object chunk = CreateFarViewChunk();
                ApplyFarViewTileChunkSource(env, chunk, firstSource, firstBounds);

                var secondBounds = new Bounds(new Vector3(1.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool rebuilt = TryBuildFarViewTileChunkSource(env, chunk, secondBounds, out var rebuiltSource);

                Assert.True(rebuilt, "Expected changed far-view capture bounds to rebuild from payload.");
                Assert.AreEqual(0, (int)GetPrivateField(env, "_farViewTileSliceCacheHits"));
                Assert.AreEqual(2, (int)GetPrivateField(env, "_farViewTileSliceCacheMisses"));
                Assert.AreNotSame(firstGroundSnapshot, GetPrivateField(rebuiltSource, "Ground"));
                AssertTilemapChunkSnapshot(
                    GetPrivateField(rebuiltSource, "Ground"),
                    "Ground",
                    new BoundsInt(1, 0, 0, 1, 1, 1),
                    1,
                    0,
                    secondTile,
                    secondTint,
                    new Vector3(1f, 0f, 0f),
                    new Vector3(2f, 1f, 0f),
                    Matrix4x4.identity);
            }
            finally
            {
                if (firstTile != null) Object.DestroyImmediate(firstTile);
                if (secondTile != null) Object.DestroyImmediate(secondTile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewTileChunkSourceDefaultApplyClearsStaleSnapshots()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            try
            {
                gridGo = new GameObject("FarViewTileChunkSourceClearGrid");
                gridGo.AddComponent<Grid>();

                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                groundGo.AddComponent<TilemapRenderer>();

                tile = ScriptableObject.CreateInstance<Tile>();
                tile.color = new Color32(96, 160, 112, 255);

                Array groundPayload = CreateNestedArray("GroundRenderCellDecision", 1);
                groundPayload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, tile, null), 0);

                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = false;
                env.FarViewIncludeProps = false;
                env.FarViewIncludeBlockers = false;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", groundPayload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 1);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_cachedGroundRenderUsesTransition", false);
                SetPrivateField(env, "_cachedFarViewTileSnapshotVersion", 23);

                var chunkBounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewTileChunkSource(env, chunkBounds, out var source);
                Assert.True(built, "Expected initial far-view tile chunk source.");

                object chunk = CreateFarViewChunk();
                ApplyFarViewTileChunkSource(env, chunk, source, chunkBounds);

                var outsideBounds = new Bounds(new Vector3(3.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool hasOutsideSource = TryBuildFarViewTileChunkSource(env, chunk, outsideBounds, out _);
                Assert.False(hasOutsideSource, "Expected no tile chunk source outside payload bounds.");

                ApplyFarViewTileChunkSource(
                    env,
                    chunk,
                    Activator.CreateInstance(GetNestedType("FarViewTileChunkSource")),
                    outsideBounds);

                object renderSource = GetPrivateField(chunk, "RenderSource");
                Assert.False((bool)GetPrivateProperty(renderSource, "HasTileSnapshots"));
                Assert.Null(GetPrivateField(renderSource, "GroundSnapshot"));
                Assert.Null(GetPrivateField(renderSource, "TransitionSnapshot"));
                Assert.Null(GetPrivateField(renderSource, "PropSnapshot"));
                Assert.Null(GetPrivateField(renderSource, "BlockerSnapshot"));
                Assert.AreEqual(0, (int)GetPrivateField(renderSource, "GroundCellCount"));
                Assert.AreEqual(0, (int)GetPrivateField(renderSource, "TransitionCellCount"));
                Assert.AreEqual(0, (int)GetPrivateField(renderSource, "PropCellCount"));
                Assert.AreEqual(0, (int)GetPrivateField(renderSource, "BlockerCellCount"));
                Assert.True((bool)GetPrivateField(renderSource, "TileSnapshotResolved"));
                Assert.AreEqual(23, (int)GetPrivateField(renderSource, "TileSnapshotVersion"));
                AssertBoundsApproximately(outsideBounds, (Bounds)GetPrivateField(renderSource, "TileSnapshotCaptureBounds"), "cleared tile capture bounds");
            }
            finally
            {
                if (tile != null) Object.DestroyImmediate(tile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewBackgroundTextureReuseRequiresVersionDimensionsCellRectAndBounds()
        {
            var env = CreateEnvironment(out var envGo);
            Texture2D texture = null;
            try
            {
                object chunk = CreateFarViewChunk();
                object renderSource = GetPrivateField(chunk, "RenderSource");
                texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
                var bounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                var cellRect = new RectInt(0, 0, 1, 1);

                SetPrivateField(chunk, "BackgroundTexture", texture);
                SetPrivateField(chunk, "CaptureBounds", bounds);
                SetPrivateField(chunk, "BackgroundRasterizedPayloadVersion", 7);
                SetPrivateField(chunk, "BackgroundRasterizedWidth", 4);
                SetPrivateField(chunk, "BackgroundRasterizedHeight", 4);
                SetPrivateField(chunk, "BackgroundRasterizedCellRect", cellRect);
                SetPrivateField(chunk, "BackgroundRasterizedCaptureBounds", bounds);

                SetPrivateField(renderSource, "BackgroundPayloadVersion", 7);
                SetPrivateField(renderSource, "BackgroundCellRect", cellRect);

                Assert.True((bool)InvokePrivateMethodWithArgs(env, "CanReuseChunkBackgroundTexture", chunk, 4, 4));

                SetPrivateField(renderSource, "BackgroundPayloadVersion", 0);
                Assert.False((bool)InvokePrivateMethodWithArgs(env, "CanReuseChunkBackgroundTexture", chunk, 4, 4), "Version zero must not reuse background cache.");

                SetPrivateField(renderSource, "BackgroundPayloadVersion", 7);
                Assert.False((bool)InvokePrivateMethodWithArgs(env, "CanReuseChunkBackgroundTexture", chunk, 5, 4), "Width mismatch must not reuse background cache.");

                SetPrivateField(renderSource, "BackgroundCellRect", new RectInt(1, 0, 1, 1));
                Assert.False((bool)InvokePrivateMethodWithArgs(env, "CanReuseChunkBackgroundTexture", chunk, 4, 4), "Cell rect mismatch must not reuse background cache.");

                SetPrivateField(renderSource, "BackgroundCellRect", cellRect);
                SetPrivateField(chunk, "CaptureBounds", new Bounds(new Vector3(1.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f)));
                Assert.False((bool)InvokePrivateMethodWithArgs(env, "CanReuseChunkBackgroundTexture", chunk, 4, 4), "Capture bounds mismatch must not reuse background cache.");
            }
            finally
            {
                if (texture != null) Object.DestroyImmediate(texture);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewTileUnderlayReuseRequiresTileAndBackgroundVersionsDimensionsAndBounds()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("FarViewTileUnderlayReuseGrid");
                gridGo.AddComponent<Grid>();

                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                groundGo.AddComponent<TilemapRenderer>();

                tile = ScriptableObject.CreateInstance<Tile>();
                Array groundPayload = CreateNestedArray("GroundRenderCellDecision", 1);
                groundPayload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, tile, null), 0);

                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = false;
                env.FarViewIncludeProps = false;
                env.FarViewIncludeBlockers = false;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", groundPayload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 1);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_cachedGroundRenderUsesTransition", false);
                SetPrivateField(env, "_cachedFarViewTileSnapshotVersion", 29);

                var bounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewTileChunkSource(env, bounds, out var source);
                Assert.True(built, "Expected tile chunk source for underlay reuse test.");

                object chunk = CreateFarViewChunk();
                ApplyFarViewTileChunkSource(env, chunk, source, bounds);
                object renderSource = GetPrivateField(chunk, "RenderSource");
                texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);

                SetPrivateField(chunk, "TileUnderlayTexture", texture);
                SetPrivateField(chunk, "CaptureBounds", bounds);
                SetPrivateField(chunk, "TileUnderlayRasterizedTileVersion", 29);
                SetPrivateField(chunk, "TileUnderlayRasterizedBackgroundVersion", -1);
                SetPrivateField(chunk, "TileUnderlayRasterizedWidth", 4);
                SetPrivateField(chunk, "TileUnderlayRasterizedHeight", 4);
                SetPrivateField(chunk, "TileUnderlayRasterizedCaptureBounds", bounds);

                Assert.True((bool)InvokePrivateMethodWithArgs(env, "CanReuseChunkTileUnderlayTexture", chunk, 4, 4));

                SetPrivateField(renderSource, "TileSnapshotVersion", 0);
                Assert.False((bool)InvokePrivateMethodWithArgs(env, "CanReuseChunkTileUnderlayTexture", chunk, 4, 4), "Version zero must not reuse tile underlay cache.");

                SetPrivateField(renderSource, "TileSnapshotVersion", 29);
                SetPrivateField(chunk, "TileUnderlayRasterizedBackgroundVersion", 3);
                Assert.False((bool)InvokePrivateMethodWithArgs(env, "CanReuseChunkTileUnderlayTexture", chunk, 4, 4), "Background payload version mismatch must not reuse tile underlay cache.");

                SetPrivateField(chunk, "TileUnderlayRasterizedBackgroundVersion", -1);
                Assert.False((bool)InvokePrivateMethodWithArgs(env, "CanReuseChunkTileUnderlayTexture", chunk, 5, 4), "Width mismatch must not reuse tile underlay cache.");

                SetPrivateField(chunk, "CaptureBounds", new Bounds(new Vector3(1.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f)));
                Assert.False((bool)InvokePrivateMethodWithArgs(env, "CanReuseChunkTileUnderlayTexture", chunk, 4, 4), "Capture bounds mismatch must not reuse tile underlay cache.");
            }
            finally
            {
                if (texture != null) Object.DestroyImmediate(texture);
                if (tile != null) Object.DestroyImmediate(tile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewPayloadOnlyPathUsesBackgroundPayloadAndClearsAfterDefaultApply()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            try
            {
                gridGo = new GameObject("FarViewPayloadOnlyBackgroundGrid");
                var backgroundGrid = gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Background");
                mapGo.transform.SetParent(gridGo.transform, false);
                var background = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                tile = ScriptableObject.CreateInstance<Tile>();
                Array payload = CreateNestedArray("BackgroundRenderCellDecision", 1);
                payload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, tile, Matrix4x4.identity), 0);

                object bakeSource = Activator.CreateInstance(GetNestedType("FarViewBakeSource"));
                SetPrivateField(bakeSource, "HasBackgroundPayload", true);
                SetPrivateField(bakeSource, "BackgroundPayload", payload);
                SetPrivateField(bakeSource, "BackgroundBounds", new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)));
                SetPrivateField(bakeSource, "BackgroundPayloadWidth", 1);
                SetPrivateField(bakeSource, "BackgroundPayloadHeight", 1);
                SetPrivateField(bakeSource, "BackgroundPayloadOrigin", Vector2Int.zero);
                SetPrivateField(bakeSource, "BackgroundPayloadVersion", 31);

                env.UseFarViewChunkedBake = true;
                env.FarViewIncludeBackground = true;
                SetPrivateField(env, "_backgroundGrid", backgroundGrid);
                SetPrivateField(env, "_background", background);

                var bounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewBackgroundChunkSource(env, bakeSource, null, bounds, out var source);
                Assert.True(built, "Expected background chunk source for payload-only route test.");

                object chunk = CreateFarViewChunk();
                SetPrivateField(chunk, "CaptureBounds", bounds);
                ApplyFarViewBackgroundChunkSource(chunk, source);

                Assert.True((bool)InvokePrivateMethodWithArgs(env, "CanUseFarViewPayloadOnlyPath", chunk));

                ApplyFarViewBackgroundChunkSource(chunk, Activator.CreateInstance(GetNestedType("FarViewBackgroundChunkSource")));
                Assert.False((bool)InvokePrivateMethodWithArgs(env, "CanUseFarViewPayloadOnlyPath", chunk), "Default background apply should clear the payload-only route when no tile snapshots exist.");
            }
            finally
            {
                if (tile != null) Object.DestroyImmediate(tile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewPayloadOnlyPathUsesTilePayloadAndClearsAfterDefaultApply()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            try
            {
                gridGo = new GameObject("FarViewPayloadOnlyTileGrid");
                gridGo.AddComponent<Grid>();

                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                groundGo.AddComponent<TilemapRenderer>();

                tile = ScriptableObject.CreateInstance<Tile>();
                Array groundPayload = CreateNestedArray("GroundRenderCellDecision", 1);
                groundPayload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, tile, null), 0);

                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = false;
                env.FarViewIncludeProps = false;
                env.FarViewIncludeBlockers = false;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", groundPayload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 1);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_cachedGroundRenderUsesTransition", false);
                SetPrivateField(env, "_cachedFarViewTileSnapshotVersion", 37);

                var bounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewTileChunkSource(env, bounds, out var source);
                Assert.True(built, "Expected tile chunk source for payload-only route test.");

                object chunk = CreateFarViewChunk();
                SetPrivateField(chunk, "CaptureBounds", bounds);
                ApplyFarViewTileChunkSource(env, chunk, source, bounds);

                Assert.True((bool)InvokePrivateMethodWithArgs(env, "CanUseFarViewPayloadOnlyPath", chunk));

                ApplyFarViewTileChunkSource(
                    env,
                    chunk,
                    Activator.CreateInstance(GetNestedType("FarViewTileChunkSource")),
                    bounds);
                Assert.False((bool)InvokePrivateMethodWithArgs(env, "CanUseFarViewPayloadOnlyPath", chunk), "Default tile apply should clear the payload-only route when no background payload exists.");
            }
            finally
            {
                if (tile != null) Object.DestroyImmediate(tile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewTileChunkSourceCombinesStreamGroundAndPlacementPayloads()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile groundTile = null;
            Tile propTile = null;
            Sprite groundSprite = null;
            Sprite propSprite = null;
            Texture2D groundTexture = null;
            Texture2D propTexture = null;
            try
            {
                gridGo = new GameObject("FarViewTileChunkSourceStreamGrid");
                gridGo.AddComponent<Grid>();

                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                groundGo.AddComponent<TilemapRenderer>();

                var propsGo = new GameObject("Props");
                propsGo.transform.SetParent(gridGo.transform, false);
                var props = propsGo.AddComponent<Tilemap>();
                propsGo.AddComponent<TilemapRenderer>();

                groundTexture = CreateSolidTexture(new Color32(48, 96, 160, 255));
                groundSprite = Sprite.Create(groundTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var groundTint = new Color32(80, 120, 200, 216);
                groundTile = ScriptableObject.CreateInstance<Tile>();
                groundTile.sprite = groundSprite;
                groundTile.color = groundTint;
                var groundTransform = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));

                propTexture = CreateSolidTexture(new Color32(160, 104, 48, 255));
                propSprite = Sprite.Create(propTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var propTint = new Color32(224, 128, 72, 200);
                propTile = ScriptableObject.CreateInstance<Tile>();
                propTile.sprite = propSprite;
                propTile.color = propTint;

                Array groundCells = CreateNestedArray("TilemapChunkCellData", 2);
                groundCells.SetValue(InvokePrivateStaticMethod("BuildTilemapChunkCellData", ground, 2, 0, groundTile, groundTransform), 0);
                groundCells.SetValue(InvokePrivateStaticMethod("BuildTilemapChunkCellData", ground, 4, 0, groundTile, Matrix4x4.identity), 1);

                var placements = (IList)CreateNestedList("Placement");
                placements.Add(CreatePlacement(2, 0, propTile));
                placements.Add(CreatePlacement(4, 0, propTile));

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "GroundPayloadBounds", new BoundsInt(2, 0, 0, 3, 1, 1));
                SetPrivateField(state, "GroundCells", groundCells);
                SetPrivateField(state, "GroundCellCount", 2);
                SetPrivateField(state, "PropPlacements", placements);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = false;
                env.FarViewIncludeProps = true;
                env.FarViewIncludeBlockers = false;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_props", props);
                SetPrivateField(env, "_streamingActive", true);

                var chunkBounds = new Bounds(new Vector3(2.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewTileChunkSource(env, chunkBounds, out var source);

                Assert.True(built, "Expected far-view tile chunk source to combine stream ground and placement payloads.");
                Assert.AreEqual(1, (int)GetPrivateProperty(source, "GroundCellCount"));
                Assert.AreEqual(1, (int)GetPrivateProperty(source, "PropCellCount"));
                Assert.Null(GetPrivateField(source, "Transitions"));
                Assert.Null(GetPrivateField(source, "Blockers"));
                var groundSnapshot = GetPrivateField(source, "Ground");
                var propSnapshot = GetPrivateField(source, "Props");
                AssertTilemapChunkSnapshot(
                    groundSnapshot,
                    "Ground",
                    new BoundsInt(2, 0, 0, 1, 1, 1),
                    2,
                    0,
                    groundTile,
                    groundTint,
                    new Vector3(2f, 0f, 0f),
                    new Vector3(3f, 1f, 0f),
                    groundTransform);
                AssertTilemapChunkSnapshot(
                    propSnapshot,
                    "Props",
                    new BoundsInt(2, 0, 0, 1, 1, 1),
                    2,
                    0,
                    propTile,
                    propTint,
                    new Vector3(2f, 0f, 0f),
                    new Vector3(3f, 1f, 0f),
                    Matrix4x4.identity);
                Assert.False(TryGetSnapshotCell(groundSnapshot, 4, 0, out _), "World bounds should clip out the second stream ground cell.");
                Assert.False(TryGetSnapshotCell(propSnapshot, 4, 0, out _), "World bounds should clip out the second stream placement.");
            }
            finally
            {
                if (groundTile != null) Object.DestroyImmediate(groundTile);
                if (propTile != null) Object.DestroyImmediate(propTile);
                if (groundSprite != null) Object.DestroyImmediate(groundSprite);
                if (propSprite != null) Object.DestroyImmediate(propSprite);
                if (groundTexture != null) Object.DestroyImmediate(groundTexture);
                if (propTexture != null) Object.DestroyImmediate(propTexture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewTileChunkSourcePrefersStreamPayloadsOverLegacyTilemaps()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile groundTile = null;
            Tile propTile = null;
            Tile transitionTile = null;
            Tile blockerTile = null;
            try
            {
                gridGo = new GameObject("FarViewTileChunkSourcePayloadFirstGrid");
                gridGo.AddComponent<Grid>();

                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                groundGo.AddComponent<TilemapRenderer>();

                var transitionsGo = new GameObject("Transitions");
                transitionsGo.transform.SetParent(gridGo.transform, false);
                var transitions = transitionsGo.AddComponent<Tilemap>();
                transitionsGo.AddComponent<TilemapRenderer>();

                var propsGo = new GameObject("Props");
                propsGo.transform.SetParent(gridGo.transform, false);
                var props = propsGo.AddComponent<Tilemap>();
                propsGo.AddComponent<TilemapRenderer>();

                var blockersGo = new GameObject("Blockers");
                blockersGo.transform.SetParent(gridGo.transform, false);
                var blockers = blockersGo.AddComponent<Tilemap>();
                blockersGo.AddComponent<TilemapRenderer>();

                groundTile = ScriptableObject.CreateInstance<Tile>();
                propTile = ScriptableObject.CreateInstance<Tile>();
                transitionTile = ScriptableObject.CreateInstance<Tile>();
                blockerTile = ScriptableObject.CreateInstance<Tile>();
                transitions.SetTile(new Vector3Int(2, 0, 0), transitionTile);
                blockers.SetTile(new Vector3Int(2, 0, 0), blockerTile);

                Array groundCells = CreateNestedArray("TilemapChunkCellData", 1);
                groundCells.SetValue(InvokePrivateStaticMethod("BuildTilemapChunkCellData", ground, 2, 0, groundTile, Matrix4x4.identity), 0);

                var placements = (IList)CreateNestedList("Placement");
                placements.Add(CreatePlacement(2, 0, propTile));

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "GroundPayloadBounds", new BoundsInt(2, 0, 0, 1, 1, 1));
                SetPrivateField(state, "GroundCells", groundCells);
                SetPrivateField(state, "GroundCellCount", 1);
                SetPrivateField(state, "PropPlacements", placements);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = true;
                env.FarViewIncludeProps = true;
                env.FarViewIncludeBlockers = true;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_transitions", transitions);
                SetPrivateField(env, "_props", props);
                SetPrivateField(env, "_blockers", blockers);
                SetPrivateField(env, "_streamingActive", true);

                var chunkBounds = new Bounds(new Vector3(2.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool built = TryBuildFarViewTileChunkSource(env, chunkBounds, out var source);

                Assert.True(built, "Expected stream payloads to produce a far-view tile chunk source.");
                Assert.AreEqual(1, (int)GetPrivateProperty(source, "GroundCellCount"));
                Assert.AreEqual(0, (int)GetPrivateProperty(source, "TransitionCellCount"), "Legacy transition tilemap should not be mixed into a stream ground payload source.");
                Assert.AreEqual(1, (int)GetPrivateProperty(source, "PropCellCount"));
                Assert.AreEqual(0, (int)GetPrivateProperty(source, "BlockerCellCount"), "Legacy blocker tilemap should not be mixed into a stream placement payload source.");
                Assert.Null(GetPrivateField(source, "Transitions"));
                Assert.Null(GetPrivateField(source, "Blockers"));
                AssertTilemapChunkSnapshot(
                    GetPrivateField(source, "Ground"),
                    "Ground",
                    new BoundsInt(2, 0, 0, 1, 1, 1),
                    2,
                    0,
                    groundTile,
                    new Color32(255, 255, 255, 255),
                    new Vector3(2f, 0f, 0f),
                    new Vector3(3f, 1f, 0f),
                    Matrix4x4.identity);
                AssertTilemapChunkSnapshot(
                    GetPrivateField(source, "Props"),
                    "Props",
                    new BoundsInt(2, 0, 0, 1, 1, 1),
                    2,
                    0,
                    propTile,
                    new Color32(255, 255, 255, 255),
                    new Vector3(2f, 0f, 0f),
                    new Vector3(3f, 1f, 0f),
                    Matrix4x4.identity);
            }
            finally
            {
                if (groundTile != null) Object.DestroyImmediate(groundTile);
                if (propTile != null) Object.DestroyImmediate(propTile);
                if (transitionTile != null) Object.DestroyImmediate(transitionTile);
                if (blockerTile != null) Object.DestroyImmediate(blockerTile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void ApplyFarViewStateKeepsBaseMapVisibleWhenContentChunkMissingSprite()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            GameObject chunkGo = null;
            try
            {
                env.UseFarViewBake = true;
                env.UseFarViewChunkedBake = true;
                env.UseBackgroundTilemap = false;

                gridGo = new GameObject("FarViewActivationGuardGrid");
                gridGo.AddComponent<Grid>();
                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                var groundRenderer = groundGo.AddComponent<TilemapRenderer>();
                groundRenderer.enabled = true;
                SetPrivateField(env, "_ground", ground);

                object chunk = CreateFarViewChunk();
                chunkGo = new GameObject("FarViewContentChunkMissingSprite");
                var chunkRenderer = chunkGo.AddComponent<SpriteRenderer>();
                SetPrivateField(chunk, "Root", chunkGo);
                SetPrivateField(chunk, "Renderer", chunkRenderer);
                SetPrivateField(chunk, "Active", true);

                object renderSource = GetPrivateField(chunk, "RenderSource");
                SetPrivateField(renderSource, "BackgroundPayload", CreateNestedArray("BackgroundRenderCellDecision", 1));

                var chunks = (IList)GetPrivateField(env, "_farViewChunks");
                chunks.Add(chunk);
                SetPrivateField(env, "_farViewHasContent", true);

                InvokePrivateMethodWithArgs(env, "ApplyFarViewState", true);
                var diagnostics = env.GetFarViewDiagnostics();

                Assert.False(diagnostics.Active, "Incomplete far-view content must not disable the base map.");
                Assert.AreEqual(1, diagnostics.ExpectedContentChunkCount);
                Assert.AreEqual(0, diagnostics.ReadyContentChunkCount);
                Assert.True(groundRenderer.enabled, "Base ground renderer should stay visible while far-view content chunks are incomplete.");
                Assert.False(chunkRenderer.enabled, "Chunk renderer without a sprite should stay disabled.");
            }
            finally
            {
                if (chunkGo != null) Object.DestroyImmediate(chunkGo);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }
    }
}
