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
        public void PlacementRenderBoundsMergeGeneratedStreamPropAndBlockerLayers()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile propTile = null;
            Tile blockerTile = null;
            try
            {
                gridGo = new GameObject("PlacementPayloadStreamBoundsGrid");
                gridGo.AddComponent<Grid>();

                var propsGo = new GameObject("Props");
                propsGo.transform.SetParent(gridGo.transform, false);
                var props = propsGo.AddComponent<Tilemap>();
                propsGo.AddComponent<TilemapRenderer>();

                var blockersGo = new GameObject("Blockers");
                blockersGo.transform.SetParent(gridGo.transform, false);
                var blockers = blockersGo.AddComponent<Tilemap>();
                blockersGo.AddComponent<TilemapRenderer>();

                propTile = ScriptableObject.CreateInstance<Tile>();
                blockerTile = ScriptableObject.CreateInstance<Tile>();

                var propPlacements = (IList)CreateNestedList("Placement");
                propPlacements.Add(CreatePlacement(2, 1, propTile));
                propPlacements.Add(CreatePlacement(4, 1, null));

                var blockerPlacements = (IList)CreateNestedList("Placement");
                blockerPlacements.Add(CreatePlacement(-1, 3, blockerTile));

                var ignoredPlacements = (IList)CreateNestedList("Placement");
                ignoredPlacements.Add(CreatePlacement(12, 12, propTile));

                Type stateType = GetNestedType("StreamChunkState");
                object generatedState = Activator.CreateInstance(stateType);
                SetPrivateField(generatedState, "Generated", true);
                SetPrivateField(generatedState, "PropPlacements", propPlacements);
                SetPrivateField(generatedState, "BlockerPlacements", blockerPlacements);

                object ignoredState = Activator.CreateInstance(stateType);
                SetPrivateField(ignoredState, "Generated", false);
                SetPrivateField(ignoredState, "PropPlacements", ignoredPlacements);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, generatedState);
                chunks.Add(new Vector2Int(1, 0), ignoredState);

                SetPrivateField(env, "_props", props);
                SetPrivateField(env, "_blockers", blockers);
                SetPrivateField(env, "_streamingActive", true);

                Assert.True(TryGetCachedPlacementRenderBounds(env, includeProps: true, includeBlockers: true, out var mergedBounds));
                AssertBoundsApproximately(
                    new Bounds(new Vector3(1f, 2.5f, 0f), new Vector3(4f, 3f, 0f)),
                    mergedBounds,
                    "merged stream placement bounds");

                Assert.True(TryGetCachedPlacementRenderBounds(env, includeProps: true, includeBlockers: false, out var propBounds));
                AssertBoundsApproximately(
                    new Bounds(new Vector3(2.5f, 1.5f, 0f), new Vector3(1f, 1f, 0f)),
                    propBounds,
                    "prop stream placement bounds");

                Assert.True(TryGetCachedPlacementRenderBounds(env, includeProps: false, includeBlockers: true, out var blockerBounds));
                AssertBoundsApproximately(
                    new Bounds(new Vector3(-0.5f, 3.5f, 0f), new Vector3(1f, 1f, 0f)),
                    blockerBounds,
                    "blocker stream placement bounds");

                Assert.False(
                    TryGetCachedPlacementRenderBounds(env, includeProps: false, includeBlockers: false, out _),
                    "Placement bounds should stay disabled when both placement layers are excluded.");
            }
            finally
            {
                if (propTile != null) Object.DestroyImmediate(propTile);
                if (blockerTile != null) Object.DestroyImmediate(blockerTile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void PlacementRenderChunkSnapshotsUseCachedPropPayloadData()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("PlacementPayloadRendererGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Props");
                mapGo.transform.SetParent(gridGo.transform, false);
                var props = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels32(new[]
                {
                    new Color32(120, 160, 96, 255),
                    new Color32(120, 160, 96, 255),
                    new Color32(120, 160, 96, 255),
                    new Color32(120, 160, 96, 255)
                });
                texture.Apply(false, false);
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(80, 144, 208, 192);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;
                tile.transform = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));

                var placements = (IList)CreateNestedList("Placement");
                placements.Add(CreatePlacement(1, 0, tile));
                placements.Add(CreatePlacement(2, 0, tile));

                env.FarViewIncludeProps = true;
                env.FarViewIncludeBlockers = false;
                SetPrivateField(env, "_props", props);
                InvokePrivateMethodWithArgs(env, "CachePropPlacementRenderPayload", CreateNestedListArray("Placement", placements));

                var worldBounds = new Bounds(new Vector3(1.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool extracted = TryExtractPlacementRenderChunkSnapshots(env, worldBounds, out var propSnapshot, out var blockerSnapshot);

                Assert.True(extracted, "Expected cached placement payload extraction to produce a prop snapshot.");
                Assert.NotNull(propSnapshot, "Expected cached prop placement snapshot.");
                Assert.Null(blockerSnapshot, "Did not request blocker placement snapshots.");
                AssertTilemapChunkSnapshot(
                    propSnapshot,
                    "Props",
                    new BoundsInt(1, 0, 0, 1, 1, 1),
                    1,
                    0,
                    tile,
                    expectedTint,
                    new Vector3(1f, 0f, 0f),
                    new Vector3(2f, 1f, 0f),
                    tile.transform);
            }
            finally
            {
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void PlacementRenderChunkSnapshotsUseStreamPropPlacementsAndWorldBounds()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("PlacementPayloadStreamGrid");
                gridGo.AddComponent<Grid>();
                var mapGo = new GameObject("Props");
                mapGo.transform.SetParent(gridGo.transform, false);
                var props = mapGo.AddComponent<Tilemap>();
                mapGo.AddComponent<TilemapRenderer>();

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels32(new[]
                {
                    new Color32(184, 128, 64, 255),
                    new Color32(184, 128, 64, 255),
                    new Color32(184, 128, 64, 255),
                    new Color32(184, 128, 64, 255)
                });
                texture.Apply(false, false);
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                var expectedTint = new Color32(208, 112, 64, 224);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = expectedTint;

                var placements = (IList)CreateNestedList("Placement");
                placements.Add(CreatePlacement(2, 0, tile));
                placements.Add(CreatePlacement(4, 0, tile));

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "PropPlacements", placements);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                env.FarViewIncludeProps = true;
                env.FarViewIncludeBlockers = false;
                SetPrivateField(env, "_props", props);
                SetPrivateField(env, "_streamingActive", true);

                var worldBounds = new Bounds(new Vector3(2.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
                bool extracted = TryExtractPlacementRenderChunkSnapshots(env, worldBounds, out var propSnapshot, out var blockerSnapshot);

                Assert.True(extracted, "Expected stream placement extraction to produce a prop snapshot.");
                Assert.NotNull(propSnapshot, "Expected stream prop placement snapshot.");
                Assert.Null(blockerSnapshot, "Did not request blocker placement snapshots.");
                AssertTilemapChunkSnapshot(
                    propSnapshot,
                    "Props",
                    new BoundsInt(2, 0, 0, 1, 1, 1),
                    2,
                    0,
                    tile,
                    expectedTint,
                    new Vector3(2f, 0f, 0f),
                    new Vector3(3f, 1f, 0f),
                    Matrix4x4.identity);
                Assert.False(TryGetSnapshotCell(propSnapshot, 4, 0, out _), "World bounds should clip out the second stream placement.");
            }
            finally
            {
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }
    }
}
