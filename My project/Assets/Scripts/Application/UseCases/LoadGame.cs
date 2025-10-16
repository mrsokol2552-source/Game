using Game.Application.Services;
using Game.Application.Ports;

namespace Game.Application.UseCases
{
    public class LoadGame
    {
        private readonly GameStateService game;
        private readonly ISaveSystem saveSystem;

        public LoadGame(GameStateService game, ISaveSystem saveSystem)
        {
            this.game = game;
            this.saveSystem = saveSystem;
        }

        public bool Execute()
        {
            return saveSystem.LoadDefault(game);
        }
    }
}
