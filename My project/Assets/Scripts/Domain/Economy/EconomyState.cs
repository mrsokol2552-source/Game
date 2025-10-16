using System;
using System.Collections.Generic;

namespace Game.Domain.Economy
{
    public class EconomyState
    {
        private readonly Dictionary<ResourceType, int> stocks = new Dictionary<ResourceType, int>();

        public event Action<ResourceType, int> OnStockChanged;

        public EconomyState()
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                stocks[type] = 0;
            }
        }

        public int GetStock(ResourceType type)
        {
            return stocks.TryGetValue(type, out var v) ? v : 0;
        }

        public void SetStock(ResourceType type, int value)
        {
            if (GetStock(type) == value) return;
            stocks[type] = value;
            OnStockChanged?.Invoke(type, value);
        }

        public void Add(ResourceType type, int delta)
        {
            if (delta == 0) return;
            SetStock(type, GetStock(type) + delta);
        }

        public bool TrySpend(ResourceType type, int amount)
        {
            if (amount <= 0) return true;
            int cur = GetStock(type);
            if (cur < amount) return false;
            SetStock(type, cur - amount);
            return true;
        }
    }
}

