using System.Collections.Generic;

namespace RtsGame.Domain.Economy
{
    public class EconomyState
    {
        private readonly Dictionary<ResourceType, int> _stocks = new();

        public int Get(ResourceType type) => _stocks.TryGetValue(type, out var v) ? v : 0;

        public void Set(ResourceType type, int amount) => _stocks[type] = amount;

        public void Add(ResourceType type, int delta) => _stocks[type] = Get(type) + delta;

        public IReadOnlyDictionary<ResourceType, int> Stocks => _stocks;
    }
}

