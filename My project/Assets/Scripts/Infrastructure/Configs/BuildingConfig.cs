using Game.Domain.Economy;
using UnityEngine;

namespace Game.Infrastructure.Configs
{
    [CreateAssetMenu(fileName = "BuildingConfig", menuName = "Configs/Building Config")]
    public class BuildingConfig : ScriptableObject
    {
        public string Id;
        public ResourceAmount[] Cost;
    }
}

