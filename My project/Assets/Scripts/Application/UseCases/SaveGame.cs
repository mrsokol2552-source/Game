using Game.Application.Services;
using Game.Application.Ports;

namespace Game.Application.UseCases
{
    public class SaveGame
    {
        private readonly GameStateService game;
        private readonly ISaveSystem saveSystem;

        public SaveGame(GameStateService game, ISaveSystem saveSystem)
        {
            this.game = game;
            this.saveSystem = saveSystem;
        }

        public void Execute()
        {
            saveSystem.SaveDefault(game);
        }
    }
}
