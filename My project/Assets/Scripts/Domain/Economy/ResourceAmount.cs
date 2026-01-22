using System;

// [CODE-ID: SCRIPTS-DOMAIN-ECONOMY-RESOURCEAMOUNT]
// Logical block: Scripts/Domain/Economy/ResourceAmount.

namespace Game.Domain.Economy
{
    [Serializable]
    public struct ResourceAmount
    {
        public ResourceType Type;
        public int Amount;

        public ResourceAmount(ResourceType type, int amount)
        {
            Type = type;
            Amount = amount;
        }
    }
}
