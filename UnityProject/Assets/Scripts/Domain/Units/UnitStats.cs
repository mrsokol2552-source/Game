using UnityEngine;

namespace RtsGame.Domain.Units
{
    [System.Serializable]
    public class UnitStats
    {
        public string Id = "unit";
        public float MaxHealth = 100f;
        public float MoveSpeed = 3.5f;
        public float AttackDamage = 10f;
        public float AttackCooldown = 1.0f;
    }
}

