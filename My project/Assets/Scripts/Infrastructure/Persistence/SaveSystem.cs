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
            public List<ResearchEntry> Research = new List<ResearchEntry>();
        }

        [Serializable]
        private struct UnitState
        {
            public float x, y, z;
            public bool hasDest;
            public float dx, dy, dz;
            public int faction;
            public int hp;
        }

        private static string DefaultPath => Path.Combine(UnityEngine.Application.persistentDataPath, "save.json");

        public struct UnitSnapshot
        {
            public Vector3 Position;
            public bool HasDestination;
            public Vector3 Destination;
            public int Faction; // Game.Domain.Units.Faction as int
            public int Health;
        }

        [Serializable]
        private struct ResearchEntry
        {
            public string id;
            public int status; // ResearchStatus as int
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
                        dz = s.Destination.z,
                        faction = s.Faction,
                        hp = s.Health
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

            // Research statuses
            var snap = game.Research.Snapshot();
            foreach (var kv in snap)
            {
                dto.Research.Add(new ResearchEntry { id = kv.Key, status = (int)kv.Value });
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
                        Destination = new Vector3(p.dx, p.dy, p.dz),
                        Faction = p.faction,
                        Health = p.hp
                    }).ToList();
                    restoreUnitsEx(states);
                }
                else if (restoreUnits != null)
                {
                    var list = dto.Units.Select(p => new Vector3(p.x, p.y, p.z)).ToList();
                    restoreUnits(list);
                }
            }
            // Restore research
            if (dto.Research != null && dto.Research.Count > 0)
            {
                var map = new System.Collections.Generic.Dictionary<string, Game.Domain.Research.ResearchStatus>();
                foreach (var e in dto.Research)
                {
                    if (string.IsNullOrEmpty(e.id)) continue;
                    map[e.id] = (Game.Domain.Research.ResearchStatus)e.status;
                }
                game.Research.ReplaceAll(map);
            }

            return true;
        }
    }
}

