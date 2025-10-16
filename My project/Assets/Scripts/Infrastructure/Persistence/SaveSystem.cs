using System;
using System.Collections.Generic;
using System.Linq;
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
            public List<UnitState> Units = new List<UnitState>();
        }

        [Serializable]
        private struct UnitState
        {
            public float x, y, z;
            public bool hasDest;
            public float dx, dy, dz;
        }

        private static string DefaultPath => Path.Combine(UnityEngine.Application.persistentDataPath, "save.json");

        public struct UnitSnapshot
        {
            public Vector3 Position;
            public bool HasDestination;
            public Vector3 Destination;
        }

        private System.Func<IEnumerable<Vector3>> captureUnits; // legacy
        private System.Action<IEnumerable<Vector3>> restoreUnits; // legacy
        private System.Func<IEnumerable<UnitSnapshot>> captureUnitsEx;
        private System.Action<IEnumerable<UnitSnapshot>> restoreUnitsEx;

        public void BindUnits(System.Func<IEnumerable<Vector3>> capture, System.Action<IEnumerable<Vector3>> restore)
        {
            captureUnits = capture;
            restoreUnits = restore;
        }

        public void BindUnitsEx(System.Func<IEnumerable<UnitSnapshot>> capture, System.Action<IEnumerable<UnitSnapshot>> restore)
        {
            captureUnitsEx = capture;
            restoreUnitsEx = restore;
        }

        public void SaveDefault(GameStateService game)
        {
            var dto = new SaveDto();
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                dto.Stocks.Add(new ResourceAmount(type, game.Economy.GetStock(type)));
            }

            if (captureUnitsEx != null)
            {
                var list = captureUnitsEx()?.ToList();
                if (list != null)
                {
                    dto.Units = list.Select(s => new UnitState
                    {
                        x = s.Position.x,
                        y = s.Position.y,
                        z = s.Position.z,
                        hasDest = s.HasDestination,
                        dx = s.Destination.x,
                        dy = s.Destination.y,
                        dz = s.Destination.z
                    }).ToList();
                }
            }
            else if (captureUnits != null)
            {
                var list = captureUnits()?.ToList();
                if (list != null)
                {
                    dto.Units = list.Select(v => new UnitState { x = v.x, y = v.y, z = v.z, hasDest = false }).ToList();
                }
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

            if (dto.Units != null)
            {
                if (restoreUnitsEx != null)
                {
                    var states = dto.Units.Select(p => new UnitSnapshot
                    {
                        Position = new Vector3(p.x, p.y, p.z),
                        HasDestination = p.hasDest,
                        Destination = new Vector3(p.dx, p.dy, p.dz)
                    }).ToList();
                    restoreUnitsEx(states);
                }
                else if (restoreUnits != null)
                {
                    var list = dto.Units.Select(p => new Vector3(p.x, p.y, p.z)).ToList();
                    restoreUnits(list);
                }
            }
            return true;
        }
    }
}

