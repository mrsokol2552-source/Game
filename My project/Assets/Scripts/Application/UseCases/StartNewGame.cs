using Game.Application.Services;
using Game.Domain.Economy;

// [CODE-ID: SCRIPTS-APPLICATION-USECASES-STARTNEWGAME]
// Logical block: Scripts/Application/UseCases/StartNewGame.

namespace Game.Application.UseCases
{
    public class StartNewGame
    {
        private readonly GameStateService game;

        public StartNewGame(GameStateService game)
        {
            this.game = game;
        }

        public void Execute(ResourceAmount[] startingResources = null)
        {
            // Reset all to zero first
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                game.Economy.SetStock(type, 0);
            }

            if (startingResources != null)
            {
                foreach (var ra in startingResources)
                {
                    game.Economy.Add(ra.Type, ra.Amount);
                }
            }
        }
    }
}
