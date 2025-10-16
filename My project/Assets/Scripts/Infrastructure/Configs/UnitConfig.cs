using Game.Domain.Economy;
using UnityEngine;

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

