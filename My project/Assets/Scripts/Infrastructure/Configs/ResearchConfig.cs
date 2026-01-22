using Game.Domain.Economy;
using UnityEngine;

// [CODE-ID: SCRIPTS-INFRASTRUCTURE-CONFIGS-RESEARCHCONFIG]
// Logical block: Scripts/Infrastructure/Configs/ResearchConfig.

namespace Game.Infrastructure.Configs
{
    [CreateAssetMenu(fileName = "ResearchConfig", menuName = "Configs/Research Config")]
    public class ResearchConfig : ScriptableObject
    {
        public ResearchDef[] Items;
    }

    [System.Serializable]
    public class ResearchDef
    {
        public string Id;
        public ResourceAmount[] Cost;
    }
}
