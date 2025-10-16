using RtsGame.Domain.Economy;

namespace RtsGame.Application.Services
{
    public class GameStateService
    {
        public EconomyState Economy { get; }

        public GameStateService(EconomyState economy)
        {
            Economy = economy;
        }
    }
}

