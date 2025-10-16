using System.Collections.Generic;
using Game.Domain.Economy;

namespace Game.Domain.Build
{
    public static class BuildingService
    {
        public static BuildResult TryPlace(EconomyState economy, IReadOnlyList<ResourceAmount> cost, out List<ResourceAmount> shortfall)
        {
            shortfall = null;
            if (economy == null || cost == null || cost.Count == 0)
                return BuildResult.InvalidRequest;

            // Calculate shortfall
            List<ResourceAmount> missing = null;
            for (int i = 0; i < cost.Count; i++)
            {
                var item = cost[i];
                int have = economy.GetStock(item.Type);
                if (have < item.Amount)
                {
                    missing ??= new List<ResourceAmount>(cost.Count);
                    missing.Add(new ResourceAmount(item.Type, item.Amount - have));
                }
            }

            if (missing != null)
            {
                shortfall = missing;
                return BuildResult.InsufficientResources;
            }

            // Spend resources
            for (int i = 0; i < cost.Count; i++)
            {
                var item = cost[i];
                economy.TrySpend(item.Type, item.Amount);
            }
            return BuildResult.Success;
        }
    }
}

