using System.Collections.Generic;
using Game.Application.Services;
using Game.Domain.Build;
using Game.Domain.Economy;
using Game.Domain.Research;
using Game.Infrastructure.Configs;

namespace Game.Application.UseCases
{
    public class StartResearch
    {
        private readonly GameStateService game;

        public StartResearch(GameStateService game)
        {
            this.game = game;
        }

        public bool Execute(ResearchDef def, out List<ResourceAmount> shortfall)
        {
            shortfall = null;
            if (def == null || string.IsNullOrEmpty(def.Id)) return false;
            var status = game.Research.GetStatus(def.Id);
            if (status == ResearchStatus.Done || status == ResearchStatus.Queued)
                return true; // already started or done

            // Pay cost (reuse building cost logic)
            var cost = (IReadOnlyList<ResourceAmount>) (def.Cost ?? new ResourceAmount[0]);
            var result = BuildingService.TryPlace(game.Economy, cost, out shortfall);
            if (result != BuildResult.Success)
                return false;

            game.Research.SetStatus(def.Id, ResearchStatus.Queued);
            return true;
        }
    }
}

