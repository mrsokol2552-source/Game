using RtsGame.Application.Services;

namespace RtsGame.Application.UseCases
{
    public static class LoadGame
    {
        public static void Execute(string json, GameStateService svc)
        {
            Infrastructure.Persistence.SaveSystem.Load(json, svc);
        }
    }
}

