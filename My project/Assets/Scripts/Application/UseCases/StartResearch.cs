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

        public bool Execute(string id, IReadOnlyList<ResourceAmount> cost, out List<ResourceAmount> shortfall)
        {
            shortfall = null;
            if (string.IsNullOrEmpty(id)) return false;
            var status = game.Research.GetStatus(id);
            if (status == ResearchStatus.Done || status == ResearchStatus.Queued)
                return true; // already started or done

            // Pay cost (reuse building cost logic)
            var list = cost ?? (IReadOnlyList<ResourceAmount>)System.Array.Empty<ResourceAmount>();
            var result = BuildingService.TryPlace(game.Economy, list, out shortfall);
            if (result != BuildResult.Success)
                return false;

            game.Research.SetStatus(id, ResearchStatus.Queued);
            return true;
        }
    }
}
