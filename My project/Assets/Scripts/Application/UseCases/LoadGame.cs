using Game.Application.Services;
using Game.Application.Ports;

// [CODE-ID: SCRIPTS-APPLICATION-USECASES-LOADGAME]
// Logical block: Scripts/Application/UseCases/LoadGame.

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