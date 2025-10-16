using Game.Domain.Economy;
using Game.Domain.Research;

namespace Game.Application.Services
{
    public class GameStateService
    {
        public EconomyState Economy { get; }
        public EconomyManager EconomyManager { get; }
        public ResearchStore Research { get; }

        public GameStateService()
        {
            Economy = new EconomyState();
            EconomyManager = new EconomyManager(Economy);
            Research = new ResearchStore();
        }
    }
}
