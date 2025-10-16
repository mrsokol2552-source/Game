using System.Collections.Generic;
using System.Linq;
using Game.Application.Services;
using Game.Application.UseCases;
using Game.Infrastructure.Configs;
using Game.Infrastructure.Persistence;
using Game.Presentation.Input;
using Game.Presentation.View;
using UnityEngine;

namespace Game.Presentation.Bootstrap
{
    public class CompositionRoot : MonoBehaviour
    {
        public static GameStateService Game { get; private set; }

        [Header("Config")]
        public GameConfig GameConfig;
        [Header("Units")]
        public UnitView DefaultUnitPrefab;
        [Header("Build")]
        public Game.Infrastructure.Configs.BuildingConfig TestBuilding;
        [Header("Research")]
        public Game.Infrastructure.Configs.ResearchConfig TestResearch;
        public bool AutoStart = true;

        private SaveSystem saveSystem;

        private void Awake()
        {
            if (Game == null)
            {
                Game = new GameStateService();
            }
            saveSystem = new SaveSystem();
            saveSystem.BindUnitsEx(CaptureUnitsEx, RestoreUnitsEx);

            if (AutoStart)
            {
                var start = new StartNewGame(Game);
                if (GameConfig != null && GameConfig.StartingResources != null)
                    start.Execute(GameConfig.StartingResources);
                else
                    start.Execute();
            }
        }

        private void Update()
        {
            // Advance domain-managed ticks; presentation supplies deltaTime.
            Game?.EconomyManager.Tick(Time.deltaTime);
        }

        public void Save()
        {
            new SaveGame(Game, saveSystem).Execute();
        }

        public void Load()
        {
            new LoadGame(Game, saveSystem).Execute();
        }

        public string LastStatusMessage { get; private set; }

        public void SetStatus(string message)
        {
            LastStatusMessage = message;
        }

        public void AttemptPlaceTestBuilding()
        {
            var cost = (TestBuilding != null && TestBuilding.Cost != null && TestBuilding.Cost.Length > 0)
                ? (IReadOnlyList<global::Game.Domain.Economy.ResourceAmount>)TestBuilding.Cost
                : new[]
                {
                    new global::Game.Domain.Economy.ResourceAmount(global::Game.Domain.Economy.ResourceType.Materials, 25),
                    new global::Game.Domain.Economy.ResourceAmount(global::Game.Domain.Economy.ResourceType.LaborHours, 5)
                };

            var useCase = new PlaceBuilding(Game);
            var result = useCase.Execute(cost, out var shortfall);
            switch (result)
            {
                case global::Game.Domain.Build.BuildResult.Success:
                    SetStatus("Build: Success (resources spent)");
                    Debug.Log("[Build] Success: resources consumed.");
                    break;
                case global::Game.Domain.Build.BuildResult.InsufficientResources:
                    string msg = "Build: Not enough resources";
                    if (shortfall != null)
                    {
                        foreach (var s in shortfall)
                            msg += $"\n- Need +{s.Amount} of {s.Type}";
                    }
                    SetStatus(msg);
                    Debug.LogWarning(msg);
                    break;
                default:
                    SetStatus("Build: Invalid request");
                    Debug.LogWarning("[Build] Invalid request");
                    break;
            }
        }

        public void AttemptStartTestResearch()
        {
            var def = (TestResearch != null && TestResearch.Items != null && TestResearch.Items.Length > 0)
                ? TestResearch.Items[0]
                : null;
            if (def == null)
            {
                SetStatus("Research: No ResearchConfig/Items set");
                return;
            }

            var start = new StartResearch(Game);
            if (start.Execute(def, out var shortfall))
            {
                SetStatus($"Research: '{def.Id}' started (Queued)");
            }
            else
            {
                string msg = $"Research: Not enough resources for '{def.Id}'";
                if (shortfall != null)
                {
                    foreach (var s in shortfall)
                        msg += $"\n- Need +{s.Amount} of {s.Type}";
                }
                SetStatus(msg);
            }
        }

        public void AttemptCompleteTestResearch()
        {
            var def = (TestResearch != null && TestResearch.Items != null && TestResearch.Items.Length > 0)
                ? TestResearch.Items[0]
                : null;
            if (def == null)
            {
                SetStatus("Research: No ResearchConfig/Items set");
                return;
            }

            var complete = new CompleteResearch(Game);
            if (complete.Execute(def))
                SetStatus($"Research: '{def.Id}' completed (Done)");
            else
                SetStatus($"Research: '{def.Id}' not started (Locked)");
        }

        private IEnumerable<SaveSystem.UnitSnapshot> CaptureUnitsEx()
        {
            var units = Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None);
            return units.Select(u =>
            {
                var snap = new SaveSystem.UnitSnapshot
                {
                    Position = u.transform.position,
                    HasDestination = u.TryGetDestination(out var d),
                    Destination = d
                };
                return snap;
            }).ToList();
        }

        private void RestoreUnitsEx(IEnumerable<SaveSystem.UnitSnapshot> states)
        {
            var existing = Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None);
            foreach (var u in existing)
            {
                if (u != null) Destroy(u.gameObject);
            }

            var prefab = DefaultUnitPrefab;
            var spawner = Object.FindFirstObjectByType<UnitSpawnerCommander>();
            if (prefab == null && spawner != null) prefab = spawner.UnitPrefab;

            if (prefab == null)
            {
                Debug.LogWarning("[CompositionRoot] No unit prefab assigned; cannot restore units.");
                return;
            }

            UnitView last = null;
            foreach (var s in states)
            {
                var u = Instantiate(prefab, s.Position, Quaternion.identity);
                if (s.HasDestination)
                    u.SetDestination(s.Destination);
                last = u;
            }

            if (spawner != null && last != null)
            {
                spawner.SetLastUnit(last);
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Game, null)) return;
            // keep Game static until domain teardown is needed
        }
    }
}
