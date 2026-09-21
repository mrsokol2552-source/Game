using System.Collections;
using System.Collections.Generic;
using Game.Presentation.Bootstrap;
using Game.Presentation.CameraControl;
using Game.Presentation.Pathfinding;
using Game.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// [CODE-ID: TESTS-PLAYMODE-SAMPLESCENEBOOTSMOKETESTS]
// Logical block: Tests/PlayMode/SampleSceneBootSmokeTests.

namespace Tests.PlayMode
{
    [Category("Gate")]
    [Category("SceneGate")]
    public class SampleSceneBootSmokeTests
    {
        private const string SampleSceneName = "SampleScene";

        [UnityTest]
        public IEnumerator SampleSceneBootsRequiredRuntimeSystems()
        {
            var failures = new List<string>();
            void CaptureUnexpectedLog(string condition, string stackTrace, LogType type)
            {
                if (IsUnexpectedFailureLog(condition, stackTrace, type))
                    failures.Add($"{type}: {condition}");
            }

            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CaptureUnexpectedLog;

            Scene loadedScene = default;
            try
            {
                yield return LoadSampleScene(failures, scene =>
                {
                    loadedScene = scene;
                    CheckSceneState(loadedScene, failures);
                });
            }
            finally
            {
                Application.logMessageReceived -= CaptureUnexpectedLog;
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            yield return CleanupLoadedScene(loadedScene);

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator SampleSceneCameraMoveExercisesStreamingSmoke()
        {
            var failures = new List<string>();
            void CaptureUnexpectedLog(string condition, string stackTrace, LogType type)
            {
                if (IsUnexpectedFailureLog(condition, stackTrace, type))
                    failures.Add($"{type}: {condition}");
            }

            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CaptureUnexpectedLog;

            Scene loadedScene = default;
            try
            {
                yield return LoadSampleScene(failures, scene => loadedScene = scene);
                if (failures.Count == 0)
                    yield return ExerciseCameraMoveStreamingSmoke(failures);
            }
            finally
            {
                Application.logMessageReceived -= CaptureUnexpectedLog;
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            yield return CleanupLoadedScene(loadedScene);

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator SampleSceneStreamingRendererToggleChurnSmoke()
        {
            var failures = new List<string>();
            void CaptureUnexpectedLog(string condition, string stackTrace, LogType type)
            {
                if (IsUnexpectedFailureLog(condition, stackTrace, type))
                    failures.Add($"{type}: {condition}");
            }

            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CaptureUnexpectedLog;

            Scene loadedScene = default;
            try
            {
                yield return LoadSampleScene(failures, scene => loadedScene = scene);
                if (failures.Count == 0)
                    yield return ExerciseStreamingRendererToggleChurnSmoke(failures);
            }
            finally
            {
                Application.logMessageReceived -= CaptureUnexpectedLog;
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            yield return CleanupLoadedScene(loadedScene);

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        private static IEnumerator LoadSampleScene(List<string> failures, System.Action<Scene> onLoaded)
        {
            var load = SceneManager.LoadSceneAsync(SampleSceneName, LoadSceneMode.Single);
            if (load == null)
            {
                failures.Add($"Could not start loading scene '{SampleSceneName}'.");
                yield break;
            }

            while (!load.isDone)
                yield return null;

            var scene = SceneManager.GetSceneByName(SampleSceneName);
            for (int i = 0; i < 3; i++)
                yield return null;

            onLoaded?.Invoke(scene);
        }

        private static IEnumerator CleanupLoadedScene(Scene loadedScene)
        {
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                yield break;

            var cleanupScene = SceneManager.CreateScene("SampleSceneBootSmokeCleanup");
            SceneManager.SetActiveScene(cleanupScene);
            var unload = SceneManager.UnloadSceneAsync(loadedScene);
            if (unload == null)
                yield break;

            while (!unload.isDone)
                yield return null;
        }

        private static void CheckSceneState(Scene scene, List<string> failures)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                failures.Add($"Scene '{SampleSceneName}' did not load.");
                return;
            }

            var compositionRoot = Object.FindAnyObjectByType<CompositionRoot>();
            if (compositionRoot == null)
                failures.Add("Missing CompositionRoot after SampleScene boot.");

            var hud = Object.FindAnyObjectByType<HudController>();
            if (hud == null)
                failures.Add("Missing HudController after SampleScene boot.");

            var cameraZoom = Object.FindAnyObjectByType<CameraZoom2D>();
            if (cameraZoom == null)
            {
                failures.Add("Missing CameraZoom2D after SampleScene boot.");
            }
            else
            {
                var camera = cameraZoom.GetComponent<Camera>();
                if (camera == null)
                    failures.Add("CameraZoom2D is not attached to a Camera.");
                else if (!camera.orthographic)
                    failures.Add("SampleScene gameplay camera is not orthographic.");
            }

            var hex = Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            if (hex == null)
            {
                failures.Add("Missing HexPathfindingBootstrap after SampleScene boot.");
            }
            else
            {
                if (hex.Width <= 0 || hex.Height <= 0)
                    failures.Add($"HexPathfindingBootstrap has invalid dimensions {hex.Width}x{hex.Height}.");
                if (hex.HexSize <= 0f)
                    failures.Add($"HexPathfindingBootstrap has invalid HexSize {hex.HexSize}.");
            }

            var environment = Object.FindAnyObjectByType<ProceduralEnvironment>();
            if (environment == null)
                failures.Add("Missing ProceduralEnvironment after SampleScene boot.");

            var pathQueue = Object.FindAnyObjectByType<PathRequestQueue>();
            if (pathQueue == null)
                failures.Add("Missing PathRequestQueue after SampleScene boot.");
        }

        private static bool IsUnexpectedFailureLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Assert && type != LogType.Exception)
                return false;

            return !IsKnownTestRunnerInfrastructureLog(condition, stackTrace);
        }

        private static bool IsKnownTestRunnerInfrastructureLog(string condition, string stackTrace)
        {
            if (string.IsNullOrEmpty(condition))
                return false;

            if (condition.Contains("An unexpected error happened while running tests."))
                return true;

            if (string.IsNullOrEmpty(stackTrace))
                return false;

            bool fromUnityTestRunner = stackTrace.Contains("UnityEditor.TestTools.TestRunner");
            if (!fromUnityTestRunner)
                return false;

            return condition.Contains("This cannot be used during play mode")
                || condition.Contains("Test tree is not available for PostbuildCleanup");
        }

        private static IEnumerator ExerciseCameraMoveStreamingSmoke(List<string> failures)
        {
            if (!TryGetStreamingSmokeContext(failures, out var cameraZoom, out var hex, out var environment))
                yield break;

            ConfigureStreamingSmoke(environment);
            var beforeMoveStreaming = environment.GetStreamingDiagnostics();
            var originalPosition = cameraZoom.transform.position;
            var currentCell = hex.WorldToGrid(originalPosition);

            if (!TryBuildStreamingRoute(hex, environment, currentCell, out var firstTargetWorld, out var secondTargetWorld))
            {
                failures.Add("Camera streaming smoke could not build a multi-step streaming route.");
                yield break;
            }

            cameraZoom.transform.position = new Vector3(firstTargetWorld.x, firstTargetWorld.y, originalPosition.z);

            yield return WaitForStreamingProgress(environment, beforeMoveStreaming, requireGeneratedChunk: true, maxFrames: 45);

            var afterFirstMoveStreaming = environment.GetStreamingDiagnostics();

            cameraZoom.transform.position = new Vector3(secondTargetWorld.x, secondTargetWorld.y, originalPosition.z);

            yield return WaitForStreamingProgress(environment, afterFirstMoveStreaming, requireGeneratedChunk: true, maxFrames: 45);

            CheckSceneState(SceneManager.GetSceneByName(SampleSceneName), failures);
            CheckStreamingState(
                environment,
                beforeMoveStreaming,
                afterFirstMoveStreaming,
                environment.GetStreamingDiagnostics(),
                failures);

            yield return ExerciseValidationRendererToggleSmoke(environment, failures);
        }

        private static IEnumerator ExerciseStreamingRendererToggleChurnSmoke(List<string> failures)
        {
            if (!TryGetStreamingSmokeContext(failures, out var cameraZoom, out var hex, out var environment))
                yield break;

            ConfigureStreamingSmoke(environment);
            SetValidationRenderersEnabled(environment, false);
            yield return WaitForValidationRendererShutdown(environment, maxFrames: 30);
            CheckExperimentalRendererRootsReleased(environment, failures, "initial churn setup");

            var originalPosition = cameraZoom.transform.position;
            var currentCell = hex.WorldToGrid(originalPosition);
            if (!TryBuildStreamingRoute(hex, environment, currentCell, out var firstTargetWorld, out var secondTargetWorld))
            {
                failures.Add("Streaming renderer churn smoke could not build a multi-step streaming route.");
                yield break;
            }

            var baseline = environment.GetStreamingDiagnostics();
            var targets = new[] { firstTargetWorld, secondTargetWorld, firstTargetWorld, secondTargetWorld };
            var observedActiveBounds = new HashSet<string>();
            int startupCount = 0;
            int shutdownCount = 0;

            for (int i = 0; i < targets.Length; i++)
            {
                bool enableRenderers = i % 2 == 0;
                SetValidationRenderersEnabled(environment, enableRenderers);

                var beforeStep = environment.GetStreamingDiagnostics();
                cameraZoom.transform.position = new Vector3(targets[i].x, targets[i].y, originalPosition.z);
                yield return null;
                yield return WaitForStreamingProgress(
                    environment,
                    beforeStep,
                    requireGeneratedChunk: i < 2,
                    maxFrames: 45);

                var afterStep = environment.GetStreamingDiagnostics();
                observedActiveBounds.Add($"{afterStep.ActiveMinChunk}:{afterStep.ActiveMaxChunk}");
                CheckStreamingStepSettled(environment, afterStep, failures, $"churn step {i + 1}");

                if (enableRenderers)
                {
                    yield return WaitForValidationRendererStartup(environment, maxFrames: 45);
                    var startup = environment.GetValidationChunkRendererDiagnostics();
                    CheckValidationRendererStartup(startup, failures, $"churn step {i + 1}");
                    if (ValidationRendererStartupSatisfied(startup))
                        startupCount++;
                }
                else
                {
                    yield return WaitForValidationRendererShutdown(environment, maxFrames: 30);
                    CheckExperimentalRendererRootsReleased(environment, failures, $"churn step {i + 1}");
                    shutdownCount++;
                }
            }

            SetValidationRenderersEnabled(environment, false);
            yield return WaitForValidationRendererShutdown(environment, maxFrames: 30);
            CheckExperimentalRendererRootsReleased(environment, failures, "final churn cleanup");

            var afterChurn = environment.GetStreamingDiagnostics();
            CheckStreamingChurnState(environment, baseline, afterChurn, observedActiveBounds.Count, startupCount, shutdownCount, failures);
        }

        private static bool TryGetStreamingSmokeContext(
            List<string> failures,
            out CameraZoom2D cameraZoom,
            out HexPathfindingBootstrap hex,
            out ProceduralEnvironment environment)
        {
            cameraZoom = Object.FindAnyObjectByType<CameraZoom2D>();
            hex = Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            environment = Object.FindAnyObjectByType<ProceduralEnvironment>();
            if (cameraZoom == null || hex == null || environment == null)
            {
                failures.Add("Cannot run camera streaming smoke without CameraZoom2D, HexPathfindingBootstrap, and ProceduralEnvironment.");
                return false;
            }

            var camera = cameraZoom.GetComponent<Camera>();
            if (camera == null || !camera.orthographic)
            {
                failures.Add("Camera streaming smoke requires an orthographic gameplay camera.");
                return false;
            }

            return true;
        }

        private static void ConfigureStreamingSmoke(ProceduralEnvironment environment)
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

        private static void CheckStreamingState(
            ProceduralEnvironment environment,
            ProceduralEnvironment.StreamingDiagnostics beforeMove,
            ProceduralEnvironment.StreamingDiagnostics afterFirstMove,
            ProceduralEnvironment.StreamingDiagnostics afterSecondMove,
            List<string> failures)
        {
            if (!environment.UseWorldStreaming)
                return;

            if (!afterSecondMove.Active)
                failures.Add("ProceduralEnvironment streaming is enabled but not active after SampleScene boot.");

            if (afterSecondMove.TrackedChunkCount <= 0)
                failures.Add($"ProceduralEnvironment streaming did not track any chunks after camera movement. {FormatStreamingDiagnostics(afterSecondMove)}");
            if (afterSecondMove.GeneratedTotal <= beforeMove.GeneratedTotal || afterSecondMove.GeneratedChunkCount <= 0)
                failures.Add($"ProceduralEnvironment streaming did not generate active chunks after camera movement. before={FormatStreamingDiagnostics(beforeMove)} after={FormatStreamingDiagnostics(afterSecondMove)}");
            if (afterSecondMove.RequiredChunkCount <= 0)
                failures.Add($"ProceduralEnvironment streaming did not report required chunk coverage after camera movement. {FormatStreamingDiagnostics(afterSecondMove)}");
            if (afterSecondMove.GeneratedTotal < beforeMove.GeneratedTotal)
                failures.Add($"ProceduralEnvironment streaming generated total regressed from {beforeMove.GeneratedTotal} to {afterSecondMove.GeneratedTotal}.");
            if (afterFirstMove.CreatedChunkStateTotal <= beforeMove.CreatedChunkStateTotal)
                failures.Add($"ProceduralEnvironment streaming did not create new chunk states after a long camera move. before={FormatStreamingDiagnostics(beforeMove)} first={FormatStreamingDiagnostics(afterFirstMove)}");
            if (afterSecondMove.ReusedChunkStateTotal <= afterFirstMove.ReusedChunkStateTotal)
                failures.Add($"ProceduralEnvironment streaming did not reuse tracked chunk states across adjacent camera moves. first={FormatStreamingDiagnostics(afterFirstMove)} second={FormatStreamingDiagnostics(afterSecondMove)}");
            if (afterSecondMove.UnloadedChunkTotal <= beforeMove.UnloadedChunkTotal)
                failures.Add($"ProceduralEnvironment streaming did not unload any chunks after camera movement with keep-generated disabled. before={FormatStreamingDiagnostics(beforeMove)} after={FormatStreamingDiagnostics(afterSecondMove)}");
            int expectedTrackedLimit = Mathf.Max(environment.StreamMaxLoadedChunks, afterSecondMove.RequiredChunkCount);
            if (afterSecondMove.TrackedChunkCount > expectedTrackedLimit)
                failures.Add($"ProceduralEnvironment streaming tracked {afterSecondMove.TrackedChunkCount} chunks, exceeding expected limit {expectedTrackedLimit}.");
            if (!StreamingActiveBoundsChanged(beforeMove, afterFirstMove))
                failures.Add($"ProceduralEnvironment streaming active chunk bounds did not change after camera movement. before={FormatStreamingDiagnostics(beforeMove)} first={FormatStreamingDiagnostics(afterFirstMove)}");
        }

        private static void CheckExperimentalRendererRootsReleased(
            ProceduralEnvironment environment,
            List<string> failures,
            string context = "renderer toggles")
        {
            var diagnostics = environment.GetValidationChunkRendererDiagnostics();
            if (diagnostics.BackgroundRootActive)
                failures.Add($"{context}: Background payload chunk renderer root remained after renderer toggles were disabled. {FormatValidationRendererDiagnostics(diagnostics)}");
            if (diagnostics.GroundRootActive)
                failures.Add($"{context}: Ground payload chunk renderer root remained after renderer toggles were disabled. {FormatValidationRendererDiagnostics(diagnostics)}");
            if (diagnostics.BiomeMaskRootActive)
                failures.Add($"{context}: Biome-mask chunk renderer root remained after renderer toggles were disabled. {FormatValidationRendererDiagnostics(diagnostics)}");
            if (diagnostics.BackgroundChunkCount != 0 || diagnostics.BackgroundQuadCount != 0)
                failures.Add($"{context}: Background payload chunk renderer counters remained after renderer toggles were disabled. {FormatValidationRendererDiagnostics(diagnostics)}");
            if (diagnostics.GroundChunkCount != 0 || diagnostics.GroundQuadCount != 0)
                failures.Add($"{context}: Ground payload chunk renderer counters remained after renderer toggles were disabled. {FormatValidationRendererDiagnostics(diagnostics)}");
            if (diagnostics.BiomeMaskChunkCount != 0 || diagnostics.BiomeMaskQuadCount != 0)
                failures.Add($"{context}: Biome-mask chunk renderer counters remained after renderer toggles were disabled. {FormatValidationRendererDiagnostics(diagnostics)}");
        }

        private static IEnumerator ExerciseValidationRendererToggleSmoke(
            ProceduralEnvironment environment,
            List<string> failures)
        {
            SetValidationRenderersEnabled(environment, true);

            yield return WaitForValidationRendererStartup(environment, maxFrames: 45);

            var startup = environment.GetValidationChunkRendererDiagnostics();
            CheckValidationRendererStartup(startup, failures, "single toggle smoke");

            SetValidationRenderersEnabled(environment, false);

            yield return WaitForValidationRendererShutdown(environment, maxFrames: 30);

            CheckExperimentalRendererRootsReleased(environment, failures);
        }

        private static void SetValidationRenderersEnabled(ProceduralEnvironment environment, bool enabled)
        {
            environment.UseBackgroundPayloadChunkRenderer = enabled;
            environment.UseGroundPayloadChunkRenderer = enabled;
            environment.UseBiomeMaskChunkRenderer = enabled;
        }

        private static void CheckValidationRendererStartup(
            ProceduralEnvironment.ValidationChunkRendererDiagnostics startup,
            List<string> failures,
            string context)
        {
            if (startup.BackgroundSourceAvailable && (!startup.BackgroundRootActive || startup.BackgroundQuadCount <= 0))
                failures.Add($"{context}: Background payload chunk renderer did not start from SampleScene payload data. {FormatValidationRendererDiagnostics(startup)}");
            if (!startup.BackgroundSourceAvailable && (startup.BackgroundRootActive || startup.BackgroundChunkCount != 0 || startup.BackgroundQuadCount != 0))
                failures.Add($"{context}: Background payload chunk renderer started without SampleScene payload source. {FormatValidationRendererDiagnostics(startup)}");
            if (startup.GroundSourceAvailable && (!startup.GroundRootActive || startup.GroundQuadCount <= 0))
                failures.Add($"{context}: Ground payload chunk renderer did not start from SampleScene payload data. {FormatValidationRendererDiagnostics(startup)}");
            if (!startup.GroundSourceAvailable && (startup.GroundRootActive || startup.GroundChunkCount != 0 || startup.GroundQuadCount != 0))
                failures.Add($"{context}: Ground payload chunk renderer started without SampleScene payload source. {FormatValidationRendererDiagnostics(startup)}");
            if (startup.BiomeMaskSourceAvailable && !startup.BiomeMaskRootActive)
                failures.Add($"{context}: Biome-mask chunk renderer did not start from SampleScene mask data. {FormatValidationRendererDiagnostics(startup)}");
            if (!startup.BiomeMaskSourceAvailable && (startup.BiomeMaskRootActive || startup.BiomeMaskChunkCount != 0 || startup.BiomeMaskQuadCount != 0))
                failures.Add($"{context}: Biome-mask chunk renderer started without SampleScene mask source. {FormatValidationRendererDiagnostics(startup)}");
        }

        private static void CheckStreamingStepSettled(
            ProceduralEnvironment environment,
            ProceduralEnvironment.StreamingDiagnostics diagnostics,
            List<string> failures,
            string context)
        {
            if (!environment.UseWorldStreaming)
                return;

            if (!diagnostics.Active)
                failures.Add($"{context}: ProceduralEnvironment streaming is enabled but not active. {FormatStreamingDiagnostics(diagnostics)}");
            if (diagnostics.PendingQueueCount != 0)
                failures.Add($"{context}: ProceduralEnvironment streaming queue did not drain inside the bounded wait. {FormatStreamingDiagnostics(diagnostics)}");
            if (diagnostics.GeneratedChunkCount <= 0)
                failures.Add($"{context}: ProceduralEnvironment has no generated active chunks after camera movement. {FormatStreamingDiagnostics(diagnostics)}");
            if (diagnostics.RequiredChunkCount <= 0)
                failures.Add($"{context}: ProceduralEnvironment did not report required chunk coverage. {FormatStreamingDiagnostics(diagnostics)}");

            int expectedTrackedLimit = Mathf.Max(environment.StreamMaxLoadedChunks, diagnostics.RequiredChunkCount);
            if (diagnostics.TrackedChunkCount > expectedTrackedLimit)
                failures.Add($"{context}: ProceduralEnvironment tracked {diagnostics.TrackedChunkCount} chunks, exceeding expected limit {expectedTrackedLimit}. {FormatStreamingDiagnostics(diagnostics)}");
        }

        private static void CheckStreamingChurnState(
            ProceduralEnvironment environment,
            ProceduralEnvironment.StreamingDiagnostics beforeChurn,
            ProceduralEnvironment.StreamingDiagnostics afterChurn,
            int observedActiveBoundsCount,
            int startupCount,
            int shutdownCount,
            List<string> failures)
        {
            if (!environment.UseWorldStreaming)
                return;

            if (afterChurn.GeneratedTotal <= beforeChurn.GeneratedTotal)
                failures.Add($"Streaming renderer churn did not generate any additional chunks. before={FormatStreamingDiagnostics(beforeChurn)} after={FormatStreamingDiagnostics(afterChurn)}");
            if (afterChurn.ReusedChunkStateTotal <= beforeChurn.ReusedChunkStateTotal)
                failures.Add($"Streaming renderer churn did not reuse chunk states across bounded camera movement. before={FormatStreamingDiagnostics(beforeChurn)} after={FormatStreamingDiagnostics(afterChurn)}");
            if (afterChurn.UnloadedChunkTotal <= beforeChurn.UnloadedChunkTotal)
                failures.Add($"Streaming renderer churn did not unload chunks with keep-generated disabled. before={FormatStreamingDiagnostics(beforeChurn)} after={FormatStreamingDiagnostics(afterChurn)}");
            if (observedActiveBoundsCount < 2)
                failures.Add($"Streaming renderer churn did not observe multiple active chunk bounds. observed={observedActiveBoundsCount} before={FormatStreamingDiagnostics(beforeChurn)} after={FormatStreamingDiagnostics(afterChurn)}");
            if (startupCount < 2)
                failures.Add($"Streaming renderer churn did not complete two renderer startup cycles. startups={startupCount}");
            if (shutdownCount < 2)
                failures.Add($"Streaming renderer churn did not complete two renderer shutdown cycles. shutdowns={shutdownCount}");
        }

        private static IEnumerator WaitForValidationRendererStartup(ProceduralEnvironment environment, int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                yield return null;

                var diagnostics = environment.GetValidationChunkRendererDiagnostics();
                if (ValidationRendererStartupSatisfied(diagnostics))
                {
                    yield break;
                }
            }
        }

        private static IEnumerator WaitForValidationRendererShutdown(ProceduralEnvironment environment, int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                yield return null;

                var diagnostics = environment.GetValidationChunkRendererDiagnostics();
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

        private static bool StreamingActiveBoundsChanged(
            ProceduralEnvironment.StreamingDiagnostics beforeMove,
            ProceduralEnvironment.StreamingDiagnostics afterMove)
        {
            return beforeMove.ActiveMinChunk != afterMove.ActiveMinChunk
                || beforeMove.ActiveMaxChunk != afterMove.ActiveMaxChunk;
        }

        private static IEnumerator WaitForStreamingProgress(
            ProceduralEnvironment environment,
            ProceduralEnvironment.StreamingDiagnostics baseline,
            bool requireGeneratedChunk,
            int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                var diagnostics = environment.GetStreamingDiagnostics();
                bool generated = !requireGeneratedChunk
                    || (diagnostics.GeneratedTotal > baseline.GeneratedTotal && diagnostics.GeneratedChunkCount > 0);
                if (generated && diagnostics.PendingQueueCount == 0)
                    yield break;

                yield return null;
            }
        }

        private static string FormatStreamingDiagnostics(ProceduralEnvironment.StreamingDiagnostics diagnostics)
        {
            return $"active={diagnostics.Active} tracked={diagnostics.TrackedChunkCount} generatedActive={diagnostics.GeneratedChunkCount} pending={diagnostics.PendingQueueCount} generatedTotal={diagnostics.GeneratedTotal} created={diagnostics.CreatedChunkStateTotal} reused={diagnostics.ReusedChunkStateTotal} unloaded={diagnostics.UnloadedChunkTotal} required={diagnostics.RequiredChunkCount} activeMin={diagnostics.ActiveMinChunk} activeMax={diagnostics.ActiveMaxChunk}";
        }

        private static string FormatValidationRendererDiagnostics(ProceduralEnvironment.ValidationChunkRendererDiagnostics diagnostics)
        {
            return $"backgroundSource={diagnostics.BackgroundSourceAvailable} backgroundRoot={diagnostics.BackgroundRootActive} backgroundChunks={diagnostics.BackgroundChunkCount} backgroundQuads={diagnostics.BackgroundQuadCount} groundSource={diagnostics.GroundSourceAvailable} groundRoot={diagnostics.GroundRootActive} groundChunks={diagnostics.GroundChunkCount} groundQuads={diagnostics.GroundQuadCount} biomeSource={diagnostics.BiomeMaskSourceAvailable} biomeRoot={diagnostics.BiomeMaskRootActive} biomeChunks={diagnostics.BiomeMaskChunkCount} biomeQuads={diagnostics.BiomeMaskQuadCount}";
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
    }
}
