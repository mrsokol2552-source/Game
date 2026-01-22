using Game.Domain.Economy;
using UnityEngine;

// [CODE-ID: SCRIPTS-INFRASTRUCTURE-CONFIGS-GAMECONFIG]
// Logical block: Scripts/Infrastructure/Configs/GameConfig.

namespace Game.Infrastructure.Configs
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Configs/Game Config")]
    public class GameConfig : ScriptableObject
    {
        public ResourceAmount[] StartingResources;
    }
}
