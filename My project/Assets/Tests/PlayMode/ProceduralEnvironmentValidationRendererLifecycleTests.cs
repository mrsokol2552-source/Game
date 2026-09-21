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
        public void ValidationChunkRendererLifecycleReleasesRootsWhenTogglesDisable()
        {
            var env = CreateEnvironment(out var envGo);
            GameObject gridGo = null;
            Tile tile = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try
            {
                gridGo = new GameObject("ValidationChunkRendererLifecycleGrid");
                var grid = gridGo.AddComponent<Grid>();

                var backgroundGo = new GameObject("Background");
                backgroundGo.transform.SetParent(gridGo.transform, false);
                var background = backgroundGo.AddComponent<Tilemap>();
                backgroundGo.AddComponent<TilemapRenderer>();

                var groundGo = new GameObject("Ground");
                groundGo.transform.SetParent(gridGo.transform, false);
                var ground = groundGo.AddComponent<Tilemap>();
                groundGo.AddComponent<TilemapRenderer>();

                texture = CreateSolidTexture(new Color32(96, 128, 160, 255));
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = new Color32(128, 160, 192, 224);

                Array backgroundPayload = CreateNestedArray("BackgroundRenderCellDecision", 1);
                backgroundPayload.SetValue(CreateBackgroundRenderCellDecision(0, 0, 0, tile, Matrix4x4.identity), 0);

                Array groundPayload = CreateNestedArray("GroundRenderCellDecision", 1);
                groundPayload.SetValue(CreateGroundRenderCellDecision(0, 0, 0, tile, null), 0);

                env.UseBackgroundTilemap = true;
                env.UseBackgroundPayloadChunkRenderer = true;
                env.BackgroundPayloadChunkRendererChunkSize = 1;
                env.UseGroundPayloadChunkRenderer = true;
                env.GroundPayloadChunkRendererChunkSize = 1;
                env.UseWaterBiome = true;
                env.UseBiomeMaskChunkRenderer = true;
                env.BiomeMaskChunkRendererChunkSize = 1;

                SetPrivateField(env, "_backgroundGrid", grid);
                SetPrivateField(env, "_background", background);
                SetPrivateField(env, "_ground", ground);
                SetPrivateField(env, "_hasCachedBackgroundFarViewPayload", true);
                SetPrivateField(env, "_cachedBackgroundFarViewPayload", backgroundPayload);
                SetPrivateField(env, "_cachedBackgroundRenderWidth", 1);
                SetPrivateField(env, "_cachedBackgroundRenderHeight", 1);
                SetPrivateField(env, "_cachedBackgroundFarViewPayloadVersion", 3);
                SetPrivateField(env, "_hasCachedGroundRenderPayload", true);
                SetPrivateField(env, "_cachedGroundRenderPayload", groundPayload);
                SetPrivateField(env, "_cachedGroundRenderWidth", 1);
                SetPrivateField(env, "_cachedGroundRenderHeight", 1);
                SetPrivateField(env, "_cachedGroundFarViewPayloadVersion", 5);
                SetPrivateField(env, "_backgroundMaskWidth", 1);
                SetPrivateField(env, "_backgroundMaskHeight", 1);
                SetPrivateField(env, "_backgroundWaterMask", new[] { true });
                SetPrivateField(env, "_backgroundRockMask", new[] { false });
                SetPrivateField(env, "_backgroundLandDistance", new[] { 0 });
                SetPrivateField(env, "_biomeMaskChunkRendererDataVersion", 7);

                InvokePrivateMethod(env, "UpdateBackgroundPayloadChunkRendererLifecycle");
                InvokePrivateMethod(env, "UpdateGroundPayloadChunkRendererLifecycle");
                InvokePrivateMethod(env, "UpdateBiomeMaskChunkRendererLifecycle");

                Assert.NotNull(GetPrivateField(env, "_backgroundPayloadChunkRendererRoot"), "Expected background renderer lifecycle to create a root.");
                Assert.NotNull(GetPrivateField(env, "_groundPayloadChunkRendererRoot"), "Expected ground renderer lifecycle to create a root.");
                Assert.NotNull(GetPrivateField(env, "_biomeMaskChunkRendererRoot"), "Expected biome renderer lifecycle to create a root.");
                Assert.AreEqual(1, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(1, (int)GetPrivateField(env, "_groundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(1, (int)GetPrivateField(env, "_biomeMaskChunkRendererQuadCount"));

                env.UseBackgroundPayloadChunkRenderer = false;
                env.UseGroundPayloadChunkRenderer = false;
                env.UseBiomeMaskChunkRenderer = false;
                InvokePrivateMethod(env, "UpdateBackgroundPayloadChunkRendererLifecycle");
                InvokePrivateMethod(env, "UpdateGroundPayloadChunkRendererLifecycle");
                InvokePrivateMethod(env, "UpdateBiomeMaskChunkRendererLifecycle");

                Assert.Null(GetPrivateField(env, "_backgroundPayloadChunkRendererRoot"), "Expected disabled background renderer toggle to clear the root reference.");
                Assert.Null(GetPrivateField(env, "_groundPayloadChunkRendererRoot"), "Expected disabled ground renderer toggle to clear the root reference.");
                Assert.Null(GetPrivateField(env, "_biomeMaskChunkRendererRoot"), "Expected disabled biome renderer toggle to clear the root reference.");
                Assert.AreEqual(0, (int)GetPrivateField(env, "_backgroundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(0, (int)GetPrivateField(env, "_groundPayloadChunkRendererQuadCount"));
                Assert.AreEqual(0, (int)GetPrivateField(env, "_biomeMaskChunkRendererQuadCount"));
            }
            finally
            {
                InvokePrivateMethod(env, "ReleaseBackgroundPayloadChunkRendererResources");
                InvokePrivateMethod(env, "ReleaseGroundPayloadChunkRendererResources");
                InvokePrivateMethod(env, "ReleaseBiomeMaskChunkRendererResources");
                if (tile != null) Object.DestroyImmediate(tile);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
                if (gridGo != null) Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(envGo);
            }
        }
    }
}
