using System.Collections;
using System.Collections.Generic;
using Game.Presentation.CameraControl;
using Game.Presentation.Pathfinding;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// [CODE-ID: TESTS-PLAYMODE-SAMPLESCENERENDERERDIAGNOSTICSTESTS]
// Logical block: Tests/PlayMode/SampleSceneRendererDiagnosticsTests.

namespace Tests.PlayMode
{
    [Category("Diagnostic")]
    [Explicit("Diagnostic baseline only: logs SampleScene renderer/streaming metrics without project performance budget assertions.")]
    public class SampleSceneRendererDiagnosticsTests
    {
        private const string SampleSceneName = "SampleScene";
        private const string ProbeLogPrefix = "[SampleSceneRendererProbe]";
        private const string ZoomOutProbeLogPrefix = "[SampleSceneZoomOutProbe]";
        private const int LoadSettleFrames = 3;
        private const int WarmupFrames = 5;

        [UnityTest]
        public IEnumerator SampleSceneRendererStreamingProbe_LogsBaselineMetrics()
        {
            var load = SceneManager.LoadSceneAsync(SampleSceneName, LoadSceneMode.Single);
            Assert.NotNull(load, $"Could not start loading scene '{SampleSceneName}'.");
            while (!load.isDone)
                yield return null;

            yield return WaitFrames(LoadSettleFrames);

            var cameraZoom = Object.FindAnyObjectByType<CameraZoom2D>();
            var hex = Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            var environment = Object.FindAnyObjectByType<ProceduralEnvironment>();
            Assert.NotNull(cameraZoom, "SampleScene renderer probe requires CameraZoom2D.");
            Assert.NotNull(hex, "SampleScene renderer probe requires HexPathfindingBootstrap.");
            Assert.NotNull(environment, "SampleScene renderer probe requires ProceduralEnvironment.");

            ConfigureProbeStreaming(environment);
            SetValidationRenderersEnabled(environment, false);

            yield return WaitFrames(WarmupFrames);

            var originalPosition = cameraZoom.transform.position;
            var currentCell = hex.WorldToGrid(originalPosition);
            Assert.IsTrue(
                TryBuildStreamingRoute(hex, environment, currentCell, out var firstTargetWorld, out var secondTargetWorld),
                "SampleScene renderer probe could not build a multi-step streaming route.");

            var metrics = new ProbeMetrics(
                environment.GetStreamingDiagnostics(),
                Profiler.GetTotalAllocatedMemoryLong(),
                System.GC.GetTotalMemory(false),
                LoadSettleFrames,
                WarmupFrames);

            var targets = new[] { firstTargetWorld, secondTargetWorld, firstTargetWorld, secondTargetWorld };
            for (int i = 0; i < targets.Length; i++)
            {
                bool enableRenderers = i % 2 == 0;
                SetValidationRenderersEnabled(environment, enableRenderers);
                yield return RecordNextFrame(metrics);

                var beforeStep = environment.GetStreamingDiagnostics();
                cameraZoom.transform.position = new Vector3(targets[i].x, targets[i].y, originalPosition.z);
                yield return WaitForStreamingSettled(environment, beforeStep, requireGeneratedChunk: i < 2, maxFrames: 45, metrics);

                if (enableRenderers)
                    yield return WaitForValidationRendererStartup(environment, maxFrames: 45, metrics);
                else
                    yield return WaitForValidationRendererShutdown(environment, maxFrames: 30, metrics);

                metrics.CaptureRendererDiagnostics(environment.GetValidationChunkRendererDiagnostics());
            }

            SetValidationRenderersEnabled(environment, false);
            yield return WaitForValidationRendererShutdown(environment, maxFrames: 30, metrics);

            metrics.Complete(
                environment.GetStreamingDiagnostics(),
                environment.GetValidationChunkRendererDiagnostics(),
                Profiler.GetTotalAllocatedMemoryLong(),
                System.GC.GetTotalMemory(false));

            Debug.Log(metrics.FormatLogLine());

            var loadedScene = SceneManager.GetSceneByName(SampleSceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                var cleanupScene = SceneManager.CreateScene("SampleSceneRendererDiagnosticsCleanup");
                SceneManager.SetActiveScene(cleanupScene);
                var unload = SceneManager.UnloadSceneAsync(loadedScene);
                while (unload != null && !unload.isDone)
                    yield return null;
            }

            Assert.Greater(metrics.MeasuredFrames, 0, "SampleScene renderer probe did not record any measured frames.");
        }

        [UnityTest]
        public IEnumerator SampleSceneMaxZoomFarViewProbe_LogsActivationMetrics()
        {
            var failures = new List<string>();
            var load = SceneManager.LoadSceneAsync(SampleSceneName, LoadSceneMode.Single);
            Assert.NotNull(load, $"Could not start loading scene '{SampleSceneName}'.");
            while (!load.isDone)
                yield return null;

            yield return WaitFrames(LoadSettleFrames);

            var cameraZoom = Object.FindAnyObjectByType<CameraZoom2D>();
            var environment = Object.FindAnyObjectByType<ProceduralEnvironment>();
            Assert.NotNull(cameraZoom, "SampleScene max-zoom probe requires CameraZoom2D.");
            Assert.NotNull(environment, "SampleScene max-zoom probe requires ProceduralEnvironment.");

            var camera = cameraZoom.GetComponent<Camera>();
            Assert.NotNull(camera, "SampleScene max-zoom probe requires a Camera on CameraZoom2D.");
            Assert.True(camera.orthographic, "SampleScene max-zoom probe requires an orthographic gameplay camera.");

            ConfigureProbeStreaming(environment);
            ConfigureZoomOutFarViewProbe(environment);
            SetValidationRenderersEnabled(environment, false);

            cameraZoom.enabled = false;
            camera.orthographicSize = cameraZoom.MaxOrthoSize;

            yield return WaitFrames(WarmupFrames);

            int frames = 0;
            float seconds = 0f;
            float maxFrameMs = 0f;
            ProceduralEnvironment.StreamingDiagnostics streaming = default;
            ProceduralEnvironment.FarViewDiagnostics farView = default;
            for (int i = 0; i < 240; i++)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f)
                {
                    frames++;
                    seconds += dt;
                    maxFrameMs = Mathf.Max(maxFrameMs, dt * 1000f);
                }

                streaming = environment.GetStreamingDiagnostics();
                farView = environment.GetFarViewDiagnostics();
                if (streaming.PendingQueueCount == 0 && farView.Active && farView.ReadyContentChunkCount == farView.ExpectedContentChunkCount)
                    break;
            }

            float avgFrameMs = frames > 0 ? (seconds / frames) * 1000f : 0f;
            Debug.Log(
                $"{ZoomOutProbeLogPrefix} measurement=maxZoomFarView ortho={camera.orthographicSize:F3} frames={frames} seconds={seconds:F3} "
                + $"avgFrameMs={avgFrameMs:F3} maxFrameMs={maxFrameMs:F3} "
                + $"streamPending={streaming.PendingQueueCount} streamGenerated={streaming.GeneratedChunkCount} streamTracked={streaming.TrackedChunkCount} streamRequired={streaming.RequiredChunkCount} "
                + $"farViewEnabled={farView.Enabled} farViewActive={farView.Active} farViewHasContent={farView.HasContent} farViewBaking={farView.Baking} "
                + $"farViewDirty={farView.Dirty} farViewChunks={farView.ChunkCount} farViewVisibleChunks={farView.VisibleChunkCount} farViewSpriteChunks={farView.SpriteChunkCount} "
                + $"farViewExpectedContentChunks={farView.ExpectedContentChunkCount} farViewReadyContentChunks={farView.ReadyContentChunkCount}");

            if (!farView.Enabled)
                failures.Add("Far-view bake is disabled in SampleScene max-zoom probe.");
            if (streaming.PendingQueueCount != 0)
                failures.Add($"Streaming queue did not drain before far-view activation. pending={streaming.PendingQueueCount} required={streaming.RequiredChunkCount}");
            if (!farView.Active || farView.VisibleChunkCount <= 0)
                failures.Add($"Far-view did not activate at max zoom. active={farView.Active} visibleChunks={farView.VisibleChunkCount} chunks={farView.ChunkCount} hasContent={farView.HasContent}");
            if (farView.ExpectedContentChunkCount <= 0)
                failures.Add("Far-view probe did not detect any content chunks.");
            if (farView.ReadyContentChunkCount != farView.ExpectedContentChunkCount)
                failures.Add($"Far-view content chunks were incomplete. ready={farView.ReadyContentChunkCount} expected={farView.ExpectedContentChunkCount}");

            yield return UnloadSampleScene();

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        private static IEnumerator WaitFrames(int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
                yield return null;
        }

        private static void ConfigureProbeStreaming(ProceduralEnvironment environment)
        {
            environment.StreamSkipIfOverBudget = false;
            environment.StreamKeepGeneratedChunks = false;
            environment.StreamBakeAllChunks = false;
            environment.StreamBakeAllChunksOnIdle = false;
            environment.StreamActiveRadius = 0;
            environment.StreamPrefetchRadius = 1;
            environment.StreamUnloadRadius = 0;
            environment.StreamMaxLoadedChunks = Mathf.Max(environment.StreamMaxLoadedChunks, 32);
            environment.StreamChunksPerFrame = Mathf.Max(environment.StreamChunksPerFrame, 64);
            environment.StreamFrameBudgetMs = 0f;
        }

        private static void ConfigureZoomOutFarViewProbe(ProceduralEnvironment environment)
        {
            environment.UseFarViewBake = true;
            environment.FarViewBakeOnStart = false;
            environment.FarViewAlwaysActive = false;
            environment.UseFarViewThresholdFromCameraZoom = true;
            environment.FarViewOrthoThresholdPercent = 0.5f;
            environment.UseFarViewChunkedBake = true;
            environment.UseFarViewDirectChunkRender = true;
            environment.UseFarViewAsyncReadback = false;
            environment.FarViewChunksPerFrame = Mathf.Max(environment.FarViewChunksPerFrame, 64);
            environment.FarViewBakeFrameBudgetMs = 0f;
            environment.FarViewChunkPixels = Mathf.Min(environment.FarViewChunkPixels, 512);
            environment.FarViewPixelsPerUnit = Mathf.Min(environment.FarViewPixelsPerUnit, 16);
        }

        private static void SetValidationRenderersEnabled(ProceduralEnvironment environment, bool enabled)
        {
            environment.UseBackgroundPayloadChunkRenderer = enabled;
            environment.UseGroundPayloadChunkRenderer = enabled;
            environment.UseBiomeMaskChunkRenderer = enabled;
        }

        private static IEnumerator RecordNextFrame(ProbeMetrics metrics)
        {
            yield return null;
            metrics.RecordFrame();
        }

        private static IEnumerator UnloadSampleScene()
        {
            var loadedScene = SceneManager.GetSceneByName(SampleSceneName);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                yield break;

            var cleanupScene = SceneManager.CreateScene("SampleSceneRendererDiagnosticsCleanup");
            SceneManager.SetActiveScene(cleanupScene);
            var unload = SceneManager.UnloadSceneAsync(loadedScene);
            while (unload != null && !unload.isDone)
                yield return null;
        }

        private static IEnumerator WaitForStreamingSettled(
            ProceduralEnvironment environment,
            ProceduralEnvironment.StreamingDiagnostics baseline,
            bool requireGeneratedChunk,
            int maxFrames,
            ProbeMetrics metrics)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                var diagnostics = environment.GetStreamingDiagnostics();
                bool generated = !requireGeneratedChunk
                    || (diagnostics.GeneratedTotal > baseline.GeneratedTotal && diagnostics.GeneratedChunkCount > 0);
                if (generated && diagnostics.PendingQueueCount == 0)
                    yield break;

                yield return null;
                metrics.RecordFrame();
            }
        }

        private static IEnumerator WaitForValidationRendererStartup(
            ProceduralEnvironment environment,
            int maxFrames,
            ProbeMetrics metrics)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                yield return null;
                metrics.RecordFrame();

                var diagnostics = environment.GetValidationChunkRendererDiagnostics();
                metrics.CaptureRendererDiagnostics(diagnostics);
                if (ValidationRendererStartupSatisfied(diagnostics))
                    yield break;
            }
        }

        private static IEnumerator WaitForValidationRendererShutdown(
            ProceduralEnvironment environment,
            int maxFrames,
            ProbeMetrics metrics)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                yield return null;
                metrics.RecordFrame();

                var diagnostics = environment.GetValidationChunkRendererDiagnostics();
                metrics.CaptureRendererDiagnostics(diagnostics);
                if (!diagnostics.BackgroundRootActive
                    && !diagnostics.GroundRootActive
                    && !diagnostics.BiomeMaskRootActive
                    && diagnostics.BackgroundChunkCount == 0
                    && diagnostics.BackgroundQuadCount == 0
                    && diagnostics.GroundChunkCount == 0
                    && diagnostics.GroundQuadCount == 0
                    && diagnostics.BiomeMaskChunkCount == 0
                    && diagnostics.BiomeMaskQuadCount == 0)
                {
                    yield break;
                }
            }
        }

        private static bool ValidationRendererStartupSatisfied(ProceduralEnvironment.ValidationChunkRendererDiagnostics diagnostics)
        {
            bool backgroundSatisfied = diagnostics.BackgroundSourceAvailable
                ? diagnostics.BackgroundRootActive && diagnostics.BackgroundQuadCount > 0
                : !diagnostics.BackgroundRootActive;
            bool groundSatisfied = diagnostics.GroundSourceAvailable
                ? diagnostics.GroundRootActive && diagnostics.GroundQuadCount > 0
                : !diagnostics.GroundRootActive;
            bool biomeMaskSatisfied = diagnostics.BiomeMaskSourceAvailable
                ? diagnostics.BiomeMaskRootActive
                : !diagnostics.BiomeMaskRootActive;

            return backgroundSatisfied && groundSatisfied && biomeMaskSatisfied;
        }

        private static bool TryBuildStreamingRoute(
            HexPathfindingBootstrap hex,
            ProceduralEnvironment environment,
            Vector2Int currentCell,
            out Vector3 firstTargetWorld,
            out Vector3 secondTargetWorld)
        {
            firstTargetWorld = default;
            secondTargetWorld = default;
            int chunkSize = Mathf.Max(8, environment.StreamChunkSize);
            int maxChunkX = Mathf.Max(0, (hex.Width - 1) / chunkSize);
            int maxChunkY = Mathf.Max(0, (hex.Height - 1) / chunkSize);
            if (maxChunkX < 3 || maxChunkY < 1)
                return false;

            int currentChunkX = Mathf.Clamp(currentCell.x / chunkSize, 0, maxChunkX);
            bool currentOnLeft = currentChunkX <= maxChunkX / 2;
            int safeMinX = Mathf.Min(1, maxChunkX);
            int safeMaxX = Mathf.Max(safeMinX, maxChunkX - 1);
            int firstChunkX = currentOnLeft ? Mathf.Max(safeMinX, safeMaxX - 1) : Mathf.Min(safeMaxX, safeMinX + 1);
            int secondChunkX = currentOnLeft ? Mathf.Max(safeMinX, firstChunkX - 1) : Mathf.Min(safeMaxX, firstChunkX + 1);

            if (firstChunkX == currentChunkX)
                firstChunkX = currentOnLeft ? safeMaxX : safeMinX;
            if (secondChunkX == firstChunkX)
                secondChunkX = currentOnLeft ? Mathf.Max(safeMinX, firstChunkX - 1) : Mathf.Min(safeMaxX, firstChunkX + 1);
            if (secondChunkX == firstChunkX)
                return false;

            int currentChunkY = Mathf.Clamp(currentCell.y / chunkSize, 0, maxChunkY);
            int routeChunkY = Mathf.Clamp(currentChunkY, 0, maxChunkY);
            int firstCol = Mathf.Clamp(firstChunkX * chunkSize + chunkSize / 2, 0, hex.Width - 1);
            int secondCol = Mathf.Clamp(secondChunkX * chunkSize + chunkSize / 2, 0, hex.Width - 1);
            int routeRow = Mathf.Clamp(routeChunkY * chunkSize + chunkSize / 2, 0, hex.Height - 1);

            firstTargetWorld = hex.GridToWorld(firstCol, routeRow);
            secondTargetWorld = hex.GridToWorld(secondCol, routeRow);
            return firstCol != secondCol;
        }

        private sealed class ProbeMetrics
        {
            private readonly ProceduralEnvironment.StreamingDiagnostics _startStreaming;
            private readonly long _startUnityAllocatedBytes;
            private readonly long _startManagedBytes;
            private readonly int _loadSettleFrames;
            private readonly int _warmupFrames;
            private ProceduralEnvironment.StreamingDiagnostics _endStreaming;
            private ProceduralEnvironment.ValidationChunkRendererDiagnostics _endRenderers;
            private long _endUnityAllocatedBytes;
            private long _endManagedBytes;

            public int MeasuredFrames { get; private set; }
            public float MeasuredSeconds { get; private set; }
            public float MaxFrameMs { get; private set; }
            public int BackgroundPeakChunks { get; private set; }
            public int BackgroundPeakQuads { get; private set; }
            public int GroundPeakChunks { get; private set; }
            public int GroundPeakQuads { get; private set; }
            public int BiomeMaskPeakChunks { get; private set; }
            public int BiomeMaskPeakQuads { get; private set; }

            public ProbeMetrics(
                ProceduralEnvironment.StreamingDiagnostics startStreaming,
                long startUnityAllocatedBytes,
                long startManagedBytes,
                int loadSettleFrames,
                int warmupFrames)
            {
                _startStreaming = startStreaming;
                _startUnityAllocatedBytes = startUnityAllocatedBytes;
                _startManagedBytes = startManagedBytes;
                _loadSettleFrames = loadSettleFrames;
                _warmupFrames = warmupFrames;
            }

            public void RecordFrame()
            {
                float dt = Time.unscaledDeltaTime;
                if (dt <= 0f)
                    return;

                MeasuredFrames++;
                MeasuredSeconds += dt;
                MaxFrameMs = Mathf.Max(MaxFrameMs, dt * 1000f);
            }

            public void CaptureRendererDiagnostics(ProceduralEnvironment.ValidationChunkRendererDiagnostics diagnostics)
            {
                BackgroundPeakChunks = Mathf.Max(BackgroundPeakChunks, diagnostics.BackgroundChunkCount);
                BackgroundPeakQuads = Mathf.Max(BackgroundPeakQuads, diagnostics.BackgroundQuadCount);
                GroundPeakChunks = Mathf.Max(GroundPeakChunks, diagnostics.GroundChunkCount);
                GroundPeakQuads = Mathf.Max(GroundPeakQuads, diagnostics.GroundQuadCount);
                BiomeMaskPeakChunks = Mathf.Max(BiomeMaskPeakChunks, diagnostics.BiomeMaskChunkCount);
                BiomeMaskPeakQuads = Mathf.Max(BiomeMaskPeakQuads, diagnostics.BiomeMaskQuadCount);
            }

            public void Complete(
                ProceduralEnvironment.StreamingDiagnostics endStreaming,
                ProceduralEnvironment.ValidationChunkRendererDiagnostics endRenderers,
                long endUnityAllocatedBytes,
                long endManagedBytes)
            {
                _endStreaming = endStreaming;
                _endRenderers = endRenderers;
                _endUnityAllocatedBytes = endUnityAllocatedBytes;
                _endManagedBytes = endManagedBytes;
            }

            public string FormatLogLine()
            {
                float avgFrameMs = MeasuredFrames > 0 ? (MeasuredSeconds / MeasuredFrames) * 1000f : 0f;
                return $"{ProbeLogPrefix} measurement=postWarmup loadSettleFrames={_loadSettleFrames} warmupFrames={_warmupFrames} frames={MeasuredFrames} seconds={MeasuredSeconds:F3} avgFrameMs={avgFrameMs:F3} maxFrameMs={MaxFrameMs:F3} "
                    + $"generatedDelta={_endStreaming.GeneratedTotal - _startStreaming.GeneratedTotal} createdDelta={_endStreaming.CreatedChunkStateTotal - _startStreaming.CreatedChunkStateTotal} reusedDelta={_endStreaming.ReusedChunkStateTotal - _startStreaming.ReusedChunkStateTotal} unloadedDelta={_endStreaming.UnloadedChunkTotal - _startStreaming.UnloadedChunkTotal} "
                    + $"finalTracked={_endStreaming.TrackedChunkCount} finalGeneratedActive={_endStreaming.GeneratedChunkCount} finalPending={_endStreaming.PendingQueueCount} finalRequired={_endStreaming.RequiredChunkCount} "
                    + $"backgroundPeakChunks={BackgroundPeakChunks} backgroundPeakQuads={BackgroundPeakQuads} groundPeakChunks={GroundPeakChunks} groundPeakQuads={GroundPeakQuads} biomePeakChunks={BiomeMaskPeakChunks} biomePeakQuads={BiomeMaskPeakQuads} "
                    + $"finalBackgroundRoot={_endRenderers.BackgroundRootActive} finalGroundRoot={_endRenderers.GroundRootActive} finalBiomeRoot={_endRenderers.BiomeMaskRootActive} "
                    + $"unityAllocDeltaMb={BytesToMegabytes(_endUnityAllocatedBytes - _startUnityAllocatedBytes):F3} managedDeltaMb={BytesToMegabytes(_endManagedBytes - _startManagedBytes):F3}";
            }

            private static float BytesToMegabytes(long bytes)
            {
                return bytes / (1024f * 1024f);
            }
        }
    }
}
