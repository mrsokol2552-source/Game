using Game.Domain.Economy;
using UnityEngine;

// [CODE-ID: SCRIPTS-INFRASTRUCTURE-CONFIGS-UNITCONFIG]
// Logical block: Scripts/Infrastructure/Configs/UnitConfig.

namespace Game.Infrastructure.Configs
{
    [CreateAssetMenu(fileName = "UnitConfig", menuName = "Configs/Unit Config")]
    public class UnitConfig : ScriptableObject
    {
        public string Id;
        public float Speed = 2f;
        public int MaxHealth = 100;
        public ResourceAmount[] Cost;
    }
}
