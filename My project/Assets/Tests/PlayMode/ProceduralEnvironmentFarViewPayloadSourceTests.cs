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
    [Category("Gate")]
    public partial class ProceduralEnvironmentFarViewPayloadSourceTests
    {
        [Test]
        public void FarViewPayloadSourcesReportDisabledWhenAllLayersAreExcluded()
        {
            var env = CreateEnvironment(out var go);
            try
            {
                env.FarViewIncludeBackground = false;
                env.FarViewIncludeGround = false;
                env.FarViewIncludeTransitions = false;
                env.FarViewIncludeProps = false;
                env.FarViewIncludeBlockers = false;

                var diagnostics = env.GetFarViewPayloadSourceDiagnostics();

                Assert.AreEqual("disabled", diagnostics.BackgroundSource);
                Assert.AreEqual("disabled", diagnostics.GroundSource);
                Assert.AreEqual("disabled", diagnostics.PlacementSource);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FarViewPayloadSourcesReportLegacyFallbacksWhenNoPayloadExists()
        {
            var env = CreateEnvironment(out var go);
            try
            {
                env.FarViewIncludeBackground = true;
                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = true;
                env.FarViewIncludeProps = true;
                env.FarViewIncludeBlockers = true;
                env.UseBackgroundTilemap = true;

                var diagnostics = env.GetFarViewPayloadSourceDiagnostics();

                Assert.AreEqual("none", diagnostics.BackgroundSource);
                Assert.AreEqual("legacy-fallback", diagnostics.GroundSource);
                Assert.AreEqual("legacy-fallback", diagnostics.PlacementSource);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FarViewPayloadSourcesPreferCachedPayloads()
        {
            var env = CreateEnvironment(out var go);
            try
            {
                env.FarViewIncludeBackground = true;
                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = true;
                env.FarViewIncludeProps = true;
                env.FarViewIncludeBlockers = true;
                env.UseBackgroundTilemap = true;

                SetPrivateField(env, "_hasCachedBackgroundFarViewPayload", true);
                SetPrivateField(env, "_cachedBackgroundFarViewPayload", CreateNestedArray("BackgroundRenderCellDecision", 1));
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", CreateNestedArray("GroundRenderCellDecision", 1));
                SetPrivateField(env, "_cachedGroundRenderWidth", 1);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_hasCachedPropPlacementPayload", true);

                var diagnostics = env.GetFarViewPayloadSourceDiagnostics();

                Assert.AreEqual("cached-ready", diagnostics.BackgroundSource);
                Assert.AreEqual("cached", diagnostics.GroundSource);
                Assert.AreEqual("cached", diagnostics.PlacementSource);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FarViewPayloadSourcesReportStreamPayloads()
        {
            var env = CreateEnvironment(out var go);
            try
            {
                env.FarViewIncludeBackground = true;
                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = false;
                env.FarViewIncludeProps = true;
                env.FarViewIncludeBlockers = false;
                env.UseBackgroundTilemap = true;

                SetPrivateField(env, "_streamingActive", true);
                AddStreamChunkWithPayloads(env);

                var diagnostics = env.GetFarViewPayloadSourceDiagnostics();

                Assert.AreEqual("stream", diagnostics.BackgroundSource);
                Assert.AreEqual("stream", diagnostics.GroundSource);
                Assert.AreEqual("stream", diagnostics.PlacementSource);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FarViewTileSnapshotBoundsPreferStreamPayloadsOverLegacyTilemaps()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            try
            {
                gridGo = new GameObject("FarViewTileSnapshotBoundsPayloadGrid");
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

                tile = ScriptableObject.CreateInstance<Tile>();
                ground.SetTile(new Vector3Int(20, 20, 0), tile);
                transitions.SetTile(new Vector3Int(30, 30, 0), tile);
                props.SetTile(new Vector3Int(40, 40, 0), tile);
                blockers.SetTile(new Vector3Int(50, 50, 0), tile);

                Array groundCells = CreateNestedArray("TilemapChunkCellData", 2);
                groundCells.SetValue(InvokePrivateStaticMethod("BuildTilemapChunkCellData", ground, 0, 0, tile, Matrix4x4.identity), 0);
                groundCells.SetValue(InvokePrivateStaticMethod("BuildTilemapChunkCellData", ground, 1, 0, tile, Matrix4x4.identity), 1);

                var placements = (IList)CreateNestedList("Placement");
                placements.Add(CreatePlacement(3, 0, tile));

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "GroundPayloadBounds", new BoundsInt(0, 0, 0, 2, 1, 1));
                SetPrivateField(state, "GroundCells", groundCells);
                SetPrivateField(state, "GroundCellCount", 2);
                SetPrivateField(state, "PropPlacements", placements);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                env.UseFarViewChunkedBake = true;
                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = true;
                env.FarViewIncludeProps = true;
                env.FarViewIncludeBlockers = true;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_transitions", transitions);
                SetPrivateField(env, "_props", props);
                SetPrivateField(env, "_blockers", blockers);
                SetPrivateField(env, "_streamingActive", true);

                bool built = TryBuildFarViewTileSnapshotBounds(env, out var bounds);

                Assert.True(built, "Expected far-view tile snapshot bounds from stream payload sources.");
                AssertBoundsApproximately(
                    new Bounds(new Vector3(2f, 0.5f, 0f), new Vector3(4f, 1f, 0f)),
                    bounds,
                    "payload-first far-view tile snapshot bounds");
            }
            finally
            {
                if (tile != null) Object.DestroyImmediate(tile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewTileSnapshotBoundsUseLegacyTilemapsWhenPayloadSourcesAreMissing()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            try
            {
                gridGo = new GameObject("FarViewTileSnapshotBoundsLegacyGrid");
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

                tile = ScriptableObject.CreateInstance<Tile>();
                ground.SetTile(new Vector3Int(1, 0, 0), tile);
                transitions.SetTile(new Vector3Int(4, 0, 0), tile);
                props.SetTile(new Vector3Int(-2, 2, 0), tile);
                blockers.SetTile(new Vector3Int(0, 3, 0), tile);

                env.UseFarViewChunkedBake = true;
                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = true;
                env.FarViewIncludeProps = true;
                env.FarViewIncludeBlockers = true;
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_transitions", transitions);
                SetPrivateField(env, "_props", props);
                SetPrivateField(env, "_blockers", blockers);

                bool built = TryBuildFarViewTileSnapshotBounds(env, out var bounds);

                Assert.True(built, "Expected far-view tile snapshot bounds from legacy tilemaps when payload sources are missing.");
                AssertBoundsApproximately(
                    new Bounds(new Vector3(1.5f, 2f, 0f), new Vector3(7f, 4f, 0f)),
                    bounds,
                    "legacy far-view tile snapshot bounds");
            }
            finally
            {
                if (tile != null) Object.DestroyImmediate(tile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }

        [Test]
        public void FarViewBakeSourceBuildsFromStreamPayloadsWithoutLiveRenderers()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            try
            {
                gridGo = new GameObject("FarViewBakeSourceStreamPayloadGrid");
                var grid = gridGo.AddComponent<Grid>();

                var backgroundGo = new GameObject("Background");
                backgroundGo.transform.SetParent(gridGo.transform, false);
                var background = backgroundGo.AddComponent<Tilemap>();
                backgroundGo.AddComponent<TilemapRenderer>();

                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                groundGo.AddComponent<TilemapRenderer>();

                var propsGo = new GameObject("Props");
                propsGo.transform.SetParent(gridGo.transform, false);
                var props = propsGo.AddComponent<Tilemap>();
                propsGo.AddComponent<TilemapRenderer>();

                tile = ScriptableObject.CreateInstance<Tile>();

                Array backgroundPayload = CreateNestedArray("BackgroundRenderCellDecision", 1);
                backgroundPayload.SetValue(CreateBackgroundRenderCellDecision(0, -1, 2, tile, Matrix4x4.identity), 0);

                Array groundCells = CreateNestedArray("TilemapChunkCellData", 2);
                groundCells.SetValue(InvokePrivateStaticMethod("BuildTilemapChunkCellData", ground, 0, 0, tile, Matrix4x4.identity), 0);
                groundCells.SetValue(InvokePrivateStaticMethod("BuildTilemapChunkCellData", ground, 1, 0, tile, Matrix4x4.identity), 1);

                var placements = (IList)CreateNestedList("Placement");
                placements.Add(CreatePlacement(3, 0, tile));

                Type stateType = GetNestedType("StreamChunkState");
                object state = Activator.CreateInstance(stateType);
                SetPrivateField(state, "Generated", true);
                SetPrivateField(state, "BackgroundPayload", backgroundPayload);
                SetPrivateField(state, "BackgroundCellCount", 1);
                SetPrivateField(state, "GroundPayloadBounds", new BoundsInt(0, 0, 0, 2, 1, 1));
                SetPrivateField(state, "GroundCells", groundCells);
                SetPrivateField(state, "GroundCellCount", 2);
                SetPrivateField(state, "PropPlacements", placements);

                var chunks = (IDictionary)GetPrivateField(env, "_streamChunks");
                chunks.Add(Vector2Int.zero, state);

                env.UseFarViewChunkedBake = true;
                env.UseBackgroundTilemap = true;
                env.FarViewIncludeBackground = true;
                env.FarViewIncludeGround = true;
                env.FarViewIncludeTransitions = false;
                env.FarViewIncludeProps = true;
                env.FarViewIncludeBlockers = false;
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_backgroundGrid", grid);
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_props", props);
                SetPrivateField(env, "_streamingActive", true);
                SetPrivateField(env, "_cachedBackgroundFarViewPayloadVersion", 17);

                bool built = TryBuildFarViewBakeSource(env, out var source);

                Assert.True(built, "Expected far-view bake source to build from stream payload sources without live renderers.");
                Assert.True((bool)GetPrivateField(source, "HasBounds"));
                Assert.True((bool)GetPrivateField(source, "HasBackgroundPayload"));
                Assert.True((bool)GetPrivateField(source, "HasTileSnapshotSource"));
                var sourceBackgroundPayload = (Array)GetPrivateField(source, "BackgroundPayload");
                Assert.NotNull(sourceBackgroundPayload);
                Assert.AreEqual(1, sourceBackgroundPayload.Length);
                Assert.AreSame(tile, (TileBase)GetPrivateField(sourceBackgroundPayload.GetValue(0), "Tile"));
                Assert.AreEqual(1, (int)GetPrivateField(source, "BackgroundPayloadWidth"));
                Assert.AreEqual(1, (int)GetPrivateField(source, "BackgroundPayloadHeight"));
                Assert.AreEqual(new Vector2Int(-1, 2), (Vector2Int)GetPrivateField(source, "BackgroundPayloadOrigin"));
                Assert.AreEqual(17, (int)GetPrivateField(source, "BackgroundPayloadVersion"));
                AssertBoundsApproximately(
                    new Bounds(new Vector3(-0.5f, 2.5f, 0f), new Vector3(1f, 1f, 0f)),
                    (Bounds)GetPrivateField(source, "BackgroundBounds"),
                    "stream background bake source bounds");
                AssertBoundsApproximately(
                    new Bounds(new Vector3(1.5f, 1.5f, 0f), new Vector3(5f, 3f, 0f)),
                    (Bounds)GetPrivateField(source, "Bounds"),
                    "combined stream payload bake source bounds");

                var renderers = (IList)GetPrivateField(source, "Renderers");
                Assert.AreEqual(0, renderers.Count, "Chunked payload-only source should remove included tilemap renderers.");
            }
            finally
            {
                if (tile != null) Object.DestroyImmediate(tile);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }
    }
}
