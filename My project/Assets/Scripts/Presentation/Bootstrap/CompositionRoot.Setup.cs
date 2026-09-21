/*
@file: My project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Setup.cs
@module: presentation.bootstrap
@purpose: Extracted bootstrap/setup helpers for CompositionRoot scene wiring.
@entry: CROOT-02
@api: CompositionRoot partial bootstrap helpers
@deps: CameraZoom2D, pathfinding bootstraps, ProceduralEnvironment, runtime singletons
@data: scene bootstrap dependencies and auto-created helper objects
@perf: startup wiring only
@thread: main thread only
@tests: manual scene bootstrap verification
@config: SampleScene references and inspector fields
@assets: auto-created helper objects
@notes: keep bootstrap helpers here to separate scene wiring from save/load and test actions
*/

using Game.Application.UseCases;
using Game.Presentation.CameraControl;
using Game.Presentation.Input;
using Game.Presentation.View;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT-SETUP]
// Logical block: CompositionRoot bootstrap/setup helper extraction.

namespace Game.Presentation.Bootstrap
{
    public partial class CompositionRoot
    {
        // [CROOT-02]
        // Bootstrap helpers, singleton wiring, and existing-scene unit setup.
        private void EnsureGameState()
        {
            if (Game == null)
            {
                Game = new global::Game.Application.Services.GameStateService();
            }
        }

        private void ExecuteAutoStartIfEnabled()
        {
            if (!AutoStart)
            {
                return;
            }

            var start = new StartNewGame(Game);
            if (GameConfig != null && GameConfig.StartingResources != null)
            {
                start.Execute(GameConfig.StartingResources);
                return;
            }

            start.Execute();
        }

        private void EnsureCameraZoomController()
        {
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<CameraZoom2D>() == null)
            {
                cam.gameObject.AddComponent<CameraZoom2D>();
            }
        }

        private void EnsurePathfindingAndEnvironment()
        {
            var hex = EnsureHexPathfindingBootstrap();
            if (hex != null)
            {
                saveSystem.BindObstacles(hex.CaptureBlocked, hex.RestoreBlocked);
            }

            EnsureProceduralObstacles();
            EnsureProceduralEnvironment();
        }

        private global::Game.Presentation.Pathfinding.HexPathfindingBootstrap EnsureHexPathfindingBootstrap()
        {
            var hex = Object.FindAnyObjectByType<global::Game.Presentation.Pathfinding.HexPathfindingBootstrap>();
            if (hex != null)
            {
                return hex;
            }

            var go = new GameObject("HexPathfinding (Auto)");
            return go.AddComponent<global::Game.Presentation.Pathfinding.HexPathfindingBootstrap>();
        }

        private void EnsureProceduralObstacles()
        {
            var obstacles = Object.FindObjectsByType<global::Game.Presentation.Pathfinding.ProceduralObstacles>(
                FindObjectsInactive.Include);
            if (obstacles.Length > 0)
            {
                return;
            }

            var go = new GameObject("ProceduralObstacles (Auto)");
            go.AddComponent<global::Game.Presentation.Pathfinding.ProceduralObstacles>();
        }

        private void EnsureProceduralEnvironment()
        {
            var environments = Object.FindObjectsByType<global::Game.Presentation.Pathfinding.ProceduralEnvironment>(
                FindObjectsInactive.Include);
            if (environments.Length > 0)
            {
                return;
            }

            var go = new GameObject("ProceduralEnvironment (Auto)");
            go.AddComponent<global::Game.Presentation.Pathfinding.ProceduralEnvironment>();
        }

        private void EnsurePerformanceSystems()
        {
            global::Game.Presentation.Performance.UnitCombatJobScheduler.EnsureExists();
            global::Game.Presentation.Performance.EnemySquadManager.EnsureExists();
            global::Game.Presentation.Performance.OccupancyHash.Ensure();
            global::Game.Presentation.Performance.LocalAvoidanceSystem.EnsureExists();
            global::Game.Presentation.Performance.OrcaAvoidanceSystem.EnsureExists();
            global::Game.Presentation.Performance.MovementJobSystem.EnsureExists();
            global::Game.Presentation.Performance.JobPipelineCoordinator.EnsureExists();
            global::Game.Presentation.Performance.UnitSoARegistry.EnsureExists();
            global::Game.Presentation.Performance.StuckResolver.EnsureExists();
            global::Game.Presentation.Pathfinding.PathRequestQueue.Ensure();
            global::Game.Presentation.Pathfinding.FlowFieldManager.EnsureExists();
            global::Game.Presentation.Pathfinding.StaticObstacleHash.EnsureExists();
            global::Game.Presentation.Pathfinding.CoverSlotHash.EnsureExists();

            var legacyAvoid = global::Game.Presentation.Performance.LocalAvoidanceSystem.Instance;
            if (legacyAvoid != null)
            {
                legacyAvoid.Enabled = false;
            }
        }

        private void ConfigureExistingSceneUnits()
        {
            var units = Object.FindObjectsByType<UnitView>();
            foreach (var unit in units)
            {
                if (unit == null)
                {
                    continue;
                }

                EnsureUnitVisualComponents(unit);
                ApplyUnitSorting(unit.GetComponent<SpriteRenderer>());
            }
        }
    }
}
