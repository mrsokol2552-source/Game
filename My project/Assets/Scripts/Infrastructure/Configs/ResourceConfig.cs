using UnityEngine;

// [CODE-ID: SCRIPTS-INFRASTRUCTURE-CONFIGS-RESOURCECONFIG]
// Logical block: Scripts/Infrastructure/Configs/ResourceConfig.

namespace Game.Infrastructure.Configs
{
    [CreateAssetMenu(fileName = "ResourceConfig", menuName = "Configs/Resource Config")]
    public class ResourceConfig : ScriptableObject
    {
        public string DisplayName;
        public Color Color = Color.white;
    }
}
