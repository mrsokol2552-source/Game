using Game.Application.Services;
using Game.Application.Ports;

// [CODE-ID: SCRIPTS-APPLICATION-USECASES-SAVEGAME]
// Logical block: Scripts/Application/UseCases/SaveGame.

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