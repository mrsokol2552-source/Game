using System.Collections.Generic;
using Game.Application.Services;
using Game.Domain.Build;
using Game.Domain.Economy;

namespace Game.Application.UseCases
{
    public class PlaceBuilding
    {
        private readonly GameStateService game;

        public PlaceBuilding(GameStateService game)
        {
            this.game = game;
        }

        public BuildResult Execute(IReadOnlyList<ResourceAmount> cost, out List<ResourceAmount> shortfall)
        {
            return BuildingService.TryPlace(game.Economy, cost, out shortfall);
        }
    }
}

