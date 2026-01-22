using Game.Application.Services;

// [CODE-ID: SCRIPTS-APPLICATION-SERVICES-ISAVESYSTEM]
// Logical block: Scripts/Application/Services/ISaveSystem.

namespace Game.Application.Ports
{
    public interface ISaveSystem
    {
        void SaveDefault(GameStateService game);
        bool LoadDefault(GameStateService game);
    }
}
