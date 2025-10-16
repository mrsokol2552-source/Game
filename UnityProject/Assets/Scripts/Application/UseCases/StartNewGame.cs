using RtsGame.Domain.Economy;
using RtsGame.Application.Services;

namespace RtsGame.Application.UseCases
{
    public static class StartNewGame
    {
        public static void Execute(GameStateService svc)
        {
            // Initialize starting resources here if needed
            svc.Economy.Set(ResourceType.LaborHours, 0);
            svc.Economy.Set(ResourceType.Power, 0);
            svc.Economy.Set(ResourceType.Materials, 0);
            svc.Economy.Set(ResourceType.Food, 0);
        }
    }
}

