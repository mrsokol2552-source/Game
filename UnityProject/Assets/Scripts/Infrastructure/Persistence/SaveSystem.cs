using System.Text.Json;
using RtsGame.Application.Services;
using RtsGame.Domain.Economy;

namespace RtsGame.Infrastructure.Persistence
{
    public static class SaveSystem
    {
        // Minimal DTOs for save
        private class EconomyDto
        {
            public int LaborHours { get; set; }
            public int Power { get; set; }
            public int Materials { get; set; }
            public int Food { get; set; }
        }

        private class SaveDto
        {
            public EconomyDto Economy { get; set; } = new();
        }

        public static string Save(GameStateService svc)
        {
            var dto = new SaveDto
            {
                Economy = new EconomyDto
                {
                    LaborHours = svc.Economy.Get(ResourceType.LaborHours),
                    Power = svc.Economy.Get(ResourceType.Power),
                    Materials = svc.Economy.Get(ResourceType.Materials),
                    Food = svc.Economy.Get(ResourceType.Food)
                }
            };
            return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        }

        public static void Load(string json, GameStateService svc)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            var dto = JsonSerializer.Deserialize<SaveDto>(json);
            if (dto == null) return;
            svc.Economy.Set(ResourceType.LaborHours, dto.Economy.LaborHours);
            svc.Economy.Set(ResourceType.Power, dto.Economy.Power);
            svc.Economy.Set(ResourceType.Materials, dto.Economy.Materials);
            svc.Economy.Set(ResourceType.Food, dto.Economy.Food);
        }
    }
}

