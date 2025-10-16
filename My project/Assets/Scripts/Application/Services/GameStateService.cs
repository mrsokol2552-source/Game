using Game.Domain.Economy;

namespace Game.Application.Services
{
    public class GameStateService
    {
        public EconomyState Economy { get; }
        public EconomyManager EconomyManager { get; }

        public GameStateService()
        {
            Economy = new EconomyState();
            EconomyManager = new EconomyManager(Economy);
        }
    }
}

