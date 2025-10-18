using System.Collections.Generic;
using Game.Application.Services;
using Game.Domain.Build;
using Game.Domain.Economy;
using Game.Domain.Research;

namespace Game.Application.UseCases
{
    public class StartResearch
    {
        private readonly GameStateService game;

        public StartResearch(GameStateService game)
        {
            this.game = game;
        }

        public ResearchStartResult Execute(string id, IReadOnlyList<ResourceAmount> cost, out List<ResourceAmount> shortfall)
        {
            shortfall = null;
            if (string.IsNullOrEmpty(id)) return ResearchStartResult.Invalid;
            var status = game.Research.GetStatus(id);
            if (status == ResearchStatus.Done)
                return ResearchStartResult.AlreadyDone;
            if (status == ResearchStatus.Queued)
                return ResearchStartResult.AlreadyQueued; // already started

            // Pay cost (reuse building cost logic)
            var list = cost ?? (IReadOnlyList<ResourceAmount>)System.Array.Empty<ResourceAmount>();
            var result = BuildingService.TryPlace(game.Economy, list, out shortfall);
            if (result != BuildResult.Success)
                return ResearchStartResult.InsufficientResources;

            game.Research.SetStatus(id, ResearchStatus.Queued);
            return ResearchStartResult.Started;
        }
    }
}
