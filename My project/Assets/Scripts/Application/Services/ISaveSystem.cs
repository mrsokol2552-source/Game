using Game.Application.Services;

namespace Game.Application.Ports
{
    public interface ISaveSystem
    {
        void SaveDefault(GameStateService game);
        bool LoadDefault(GameStateService game);
    }
}

