/*
@file: My project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.cs
@module: presentation.bootstrap
@purpose: Wires scene-level services, save/load hooks, configs, and default unit/building assets into the runtime.
@entry: CompositionRoot.Awake, CROOT-01
@api: scene bootstrap MonoBehaviour
@deps: GameStateService, SaveSystem, configs, input/UI systems, prefabs
@data: top-level service graph and scene-bound asset references
@perf: not a hotpath; startup wiring only
@thread: main thread only
@tests: manual scene bootstrap verification
@config: SampleScene references and inspector fields
@assets: GameConfig, unit prefabs, sprites, research/build configs
@notes: incorrect scene references here tend to surface as missing runtime systems rather than compile errors
*/

using Game.Application.Services;
using Game.Infrastructure.Configs;
using Game.Infrastructure.Persistence;
using Game.Presentation.View;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT]
// Logical block: Scripts/Presentation/Bootstrap/CompositionRoot.

namespace Game.Presentation.Bootstrap
{
    public partial class CompositionRoot : MonoBehaviour
    {
        // [CROOT-01]
        // Core lifecycle, shared inspector references, and root service ownership.
        public static GameStateService Game { get; private set; }

        [Header("Config")]
        public GameConfig GameConfig;
        [Header("Units")]
        public UnitView DefaultUnitPrefab;
        [Header("Build")]
        public Game.Infrastructure.Configs.BuildingConfig TestBuilding;
        [Header("Visuals")]
        public Sprite PlayerSprite;
        public Sprite EnemySprite;
        [Header("Rendering")]
        public string UnitSortingLayerName = "Units";
        public int UnitSortingOrder = 0;
        [Header("Research")]
        public Game.Infrastructure.Configs.ResearchConfig TestResearch;
        public bool AutoStart = true;

        private SaveSystem saveSystem;

        private void Awake()
        {
            EnsureGameState();
            saveSystem = new SaveSystem();
            saveSystem.BindUnitsEx(CaptureUnitsEx, RestoreUnitsEx);

            ExecuteAutoStartIfEnabled();
            EnsureCameraZoomController();
            EnsurePathfindingAndEnvironment();
            EnsurePerformanceSystems();
            ConfigureExistingSceneUnits();
        }

        private void Update()
        {
            // Advance domain-managed ticks; presentation supplies deltaTime.
            Game?.EconomyManager.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Game, null)) return;
            // keep Game static until domain teardown is needed
        }
    }
}


