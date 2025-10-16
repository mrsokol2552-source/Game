using Game.Domain.Economy;
using UnityEngine;

namespace Game.Infrastructure.Configs
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Configs/Game Config")]
    public class GameConfig : ScriptableObject
    {
        public ResourceAmount[] StartingResources;
    }
}

