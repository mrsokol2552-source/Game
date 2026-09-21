/*
@file: My project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Actions.cs
@module: presentation.bootstrap
@purpose: Extracted status, save/load, and debug test actions for CompositionRoot.
@entry: CROOT-03
@api: CompositionRoot partial action helpers
@deps: save/load use cases, build use cases, research use cases
@data: transient status text and inspector-provided test assets
@perf: UI/debug actions only
@thread: main thread only
@tests: manual UI smoke test via ActionsPanel and ResearchPanel
@config: TestBuilding, TestResearch, AutoStart
@assets: building config and research config references
@notes: keep debug actions separate from startup wiring so bootstrap flow stays compact
*/

using System.Collections.Generic;
using Game.Application.UseCases;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT-ACTIONS]
// Logical block: CompositionRoot save/load and debug action extraction.

namespace Game.Presentation.Bootstrap
{
    public partial class CompositionRoot
    {
        // [CROOT-03]
        // Save/load wrappers, status text, and test actions.
        public string LastStatusMessage { get; private set; }

        public void Save()
        {
            new SaveGame(Game, saveSystem).Execute();
        }

        public void Load()
        {
            new LoadGame(Game, saveSystem).Execute();
        }

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
                    var buildMessage = "Build: Not enough resources";
                    if (shortfall != null)
                    {
                        foreach (var shortage in shortfall)
                        {
                            buildMessage += $"\n- Need +{shortage.Amount} of {shortage.Type}";
                        }
                    }

                    SetStatus(buildMessage);
                    Debug.LogWarning(buildMessage);
                    break;
                default:
                    SetStatus("Build: Invalid request");
                    Debug.LogWarning("[Build] Invalid request");
                    break;
            }
        }

        public void AttemptStartTestResearch()
        {
            var definition = GetPrimaryResearchDefinition();
            if (definition == null)
            {
                SetStatus("Research: No ResearchConfig/Items set");
                return;
            }

            var start = new StartResearch(Game);
            var cost = (IReadOnlyList<global::Game.Domain.Economy.ResourceAmount>)(definition.Cost ?? System.Array.Empty<global::Game.Domain.Economy.ResourceAmount>());
            var result = start.Execute(definition.Id, cost, out var shortfall);
            switch (result)
            {
                case global::Game.Domain.Research.ResearchStartResult.Started:
                    SetStatus($"Research: '{definition.Id}' started (Queued)");
                    break;
                case global::Game.Domain.Research.ResearchStartResult.AlreadyQueued:
                    SetStatus($"Research: '{definition.Id}' already queued");
                    break;
                case global::Game.Domain.Research.ResearchStartResult.AlreadyDone:
                    SetStatus($"Research: '{definition.Id}' already done");
                    break;
                case global::Game.Domain.Research.ResearchStartResult.InsufficientResources:
                    var researchMessage = $"Research: Not enough resources for '{definition.Id}'";
                    if (shortfall != null)
                    {
                        foreach (var shortage in shortfall)
                        {
                            researchMessage += $"\n- Need +{shortage.Amount} of {shortage.Type}";
                        }
                    }

                    SetStatus(researchMessage);
                    break;
                default:
                    SetStatus("Research: invalid request");
                    break;
            }
        }

        public void AttemptCompleteTestResearch()
        {
            var definition = GetPrimaryResearchDefinition();
            if (definition == null)
            {
                SetStatus("Research: No ResearchConfig/Items set");
                return;
            }

            var complete = new CompleteResearch(Game);
            if (complete.Execute(definition.Id))
            {
                SetStatus($"Research: '{definition.Id}' completed (Done)");
                return;
            }

            SetStatus($"Research: '{definition.Id}' not started (Locked)");
        }

        private global::Game.Infrastructure.Configs.ResearchDef GetPrimaryResearchDefinition()
        {
            if (TestResearch == null || TestResearch.Items == null || TestResearch.Items.Length == 0)
            {
                return null;
            }

            return TestResearch.Items[0];
        }
    }
}
