using RtsGame.Application.Services;

namespace RtsGame.Application.UseCases
{
    public static class SaveGame
    {
        public static string Execute(GameStateService svc)
        {
            // Infrastructure.SaveSystem can be called by the presentation layer
            return Infrastructure.Persistence.SaveSystem.Save(svc);
        }
    }
}

