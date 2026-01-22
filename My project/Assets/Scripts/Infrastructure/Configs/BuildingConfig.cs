using Game.Domain.Economy;
using UnityEngine;

// [CODE-ID: SCRIPTS-INFRASTRUCTURE-CONFIGS-BUILDINGCONFIG]
// Logical block: Scripts/Infrastructure/Configs/BuildingConfig.

namespace Game.Infrastructure.Configs
{
    [CreateAssetMenu(fileName = "BuildingConfig", menuName = "Configs/Building Config")]
    public class BuildingConfig : ScriptableObject
    {
        public string Id;
        public ResourceAmount[] Cost;
    }
}
