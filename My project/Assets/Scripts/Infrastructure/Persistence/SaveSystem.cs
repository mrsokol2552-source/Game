using System;
using System.Collections.Generic;
using System.IO;
using Game.Application.Services;
using Game.Application.Ports;
using Game.Domain.Economy;
using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    public class SaveSystem : ISaveSystem
    {
        [Serializable]
        private class SaveDto
        {
            public int Version = 1;
            public List<ResourceAmount> Stocks = new List<ResourceAmount>();
        }

        private static string DefaultPath => Path.Combine(UnityEngine.Application.persistentDataPath, "save.json");

        public void SaveDefault(GameStateService game)
        {
            var dto = new SaveDto();
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                dto.Stocks.Add(new ResourceAmount(type, game.Economy.GetStock(type)));
            }

            var json = JsonUtility.ToJson(dto, prettyPrint: true);
            var dir = Path.GetDirectoryName(DefaultPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(DefaultPath, json);
        }

        public bool LoadDefault(GameStateService game)
        {
            if (!File.Exists(DefaultPath)) return false;
            var json = File.ReadAllText(DefaultPath);
            var dto = JsonUtility.FromJson<SaveDto>(json);
            if (dto == null) return false;
            foreach (var ra in dto.Stocks)
            {
                game.Economy.SetStock(ra.Type, ra.Amount);
            }
            return true;
        }
    }
}

