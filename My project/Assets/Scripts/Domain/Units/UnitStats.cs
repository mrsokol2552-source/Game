using System;

// [CODE-ID: SCRIPTS-DOMAIN-UNITS-UNITSTATS]
// Logical block: Scripts/Domain/Units/UnitStats.

namespace Game.Domain.Units
{
    [Serializable]
    public class UnitStats
    {
        public float Speed = 2f;
        public int MaxHealth = 100;
    }
}