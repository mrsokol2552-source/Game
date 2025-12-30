using UnityEngine;

namespace Game.Infrastructure.Configs
{
    [CreateAssetMenu(menuName = "Configs/Unit Behavior Profile", fileName = "UnitBehaviorProfile")]
    public class UnitBehaviorProfile : ScriptableObject
    {
        [Header("Behavior")]
        public bool HoldPosition = false;
        public bool UseAggroRange = false;
        public float AggroRange = 12f;
        public bool UseLeash = false;
        public float LeashRange = 20f;
        public bool PreferForcedTarget = false;
    }
}
