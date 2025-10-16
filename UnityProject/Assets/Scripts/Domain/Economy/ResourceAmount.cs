using System;

namespace RtsGame.Domain.Economy
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

