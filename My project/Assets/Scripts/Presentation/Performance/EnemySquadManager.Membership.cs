/*
@file: My project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Membership.cs
@module: presentation.combat.squads
@purpose: Extracted squad membership, recruitment, and anchor-center maintenance for EnemySquadManager.
@entry: ESQD-04
@api: EnemySquadManager partial squad-composition helpers
@deps: UnitCombat squad membership API and hex-grid cell conversion
@data: free-unit buffers, squad member lists, gather radii, anchor centers
@perf: hotpath during large battle regrouping and squad formation
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual regrouping verification
@config: MaxSquadSize, gather radius settings, DrivePlayers
@assets: none
@notes: this layer owns who belongs to a squad; tactical state changes are handled separately
*/

using System.Collections.Generic;
using UnityEngine;
using Game.Domain.Units;
using Game.Presentation.View;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER-MEMBERSHIP]
// Logical block: EnemySquadManager squad membership/recruitment extraction.

namespace Game.Presentation.Performance
{
    public partial class EnemySquadManager
    {
        // [ESQD-04]
        // Squad composition, member recruitment, and squad-center maintenance.
        private void UpdateSquads(List<Squad> squads)
        {
            for (int i = squads.Count - 1; i >= 0; i--)
            {
                var squad = squads[i];
                if (squad == null)
                {
                    squads.RemoveAt(i);
                    continue;
                }

                for (int memberIndex = squad.Members.Count - 1; memberIndex >= 0; memberIndex--)
                {
                    var unit = squad.Members[memberIndex];
                    if (unit == null || !unit.isActiveAndEnabled)
                    {
                        squad.Members.RemoveAt(memberIndex);
                    }
                }

                if (squad.Members.Count == 0)
                {
                    squads.RemoveAt(i);
                    continue;
                }

                squad.Center = ComputeCenter(squad.Members);
                squad.CenterCell = WorldToCell(squad.Center);
            }
        }

        private void GatherFreeUnits()
        {
            _freePlayers.Clear();
            _freeEnemies.Clear();
            _activeSquadIds.Clear();

            for (int i = 0; i < _playerSquads.Count; i++)
            {
                _activeSquadIds.Add(_playerSquads[i].Id);
            }

            for (int i = 0; i < _enemySquads.Count; i++)
            {
                _activeSquadIds.Add(_enemySquads[i].Id);
            }

            foreach (var unit in UnitCombat.All)
            {
                if (unit == null || !unit.isActiveAndEnabled)
                {
                    continue;
                }

                if (unit.IsInSquad && !_activeSquadIds.Contains(unit.SquadId))
                {
                    unit.ClearSquad();
                }

                if (unit.IsInSquad)
                {
                    continue;
                }

                if (unit.Faction == Faction.Player)
                {
                    _freePlayers.Add(unit);
                }
                else if (unit.Faction == Faction.Enemy)
                {
                    _freeEnemies.Add(unit);
                }
            }
        }

        private void FillSquads(List<Squad> squads, List<UnitCombat> free)
        {
            if (squads.Count == 0 || free.Count == 0)
            {
                return;
            }

            for (int i = 0; i < squads.Count && free.Count > 0; i++)
            {
                var squad = squads[i];
                if (squad == null || squad.Members.Count >= MaxSquadSize)
                {
                    continue;
                }

                var isSleeping = squad.Mode == UnitCombat.SquadMode.Sleeping;
                if (isSleeping && Time.time < squad.SleepUntil)
                {
                    continue;
                }

                if (isSleeping && Time.time >= squad.SleepUntil)
                {
                    squad.Mode = UnitCombat.SquadMode.Gathering;
                    squad.LastStateChangeTime = Time.time;
                }

                if (Time.time >= squad.NextRadiusGrowTime)
                {
                    var step = Mathf.Max(1, GatherRadiusStepHex);
                    var maxRadius = Mathf.Max(1, MaxGatherRadiusHex);
                    squad.GatherRadiusHex = Mathf.Min(maxRadius, squad.GatherRadiusHex + step);
                    squad.NextRadiusGrowTime = Time.time + Mathf.Max(0.05f, GatherRadiusStepSeconds);
                }

                TryRecruit(squad, free);

                if (squad.Members.Count < MaxSquadSize &&
                    squad.GatherRadiusHex >= MaxGatherRadiusHex &&
                    (squad.Mode == UnitCombat.SquadMode.Gathering || squad.Mode == UnitCombat.SquadMode.Marching))
                {
                    squad.Mode = UnitCombat.SquadMode.Sleeping;
                    squad.SleepUntil = Time.time + Mathf.Max(0.5f, SleepRetrySeconds);
                    squad.LastStateChangeTime = Time.time;
                }
            }
        }

        private void CreateSquadsFromFree(List<Squad> squads, List<UnitCombat> free, Faction faction)
        {
            if (free.Count == 0)
            {
                return;
            }

            var initialRadius = Mathf.Max(1, InitialGatherRadiusHex);
            var maxRadius = Mathf.Max(initialRadius, MaxGatherRadiusHex);

            while (free.Count > 0)
            {
                var leader = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
                if (leader == null || !leader.isActiveAndEnabled)
                {
                    continue;
                }

                var squad = new Squad
                {
                    Id = _nextSquadId++,
                    Faction = faction,
                    Mode = UnitCombat.SquadMode.Gathering,
                    GatherRadiusHex = initialRadius,
                    NextRadiusGrowTime = Time.time + Mathf.Max(0.05f, GatherRadiusStepSeconds)
                };
                squad.Members.Add(leader);
                leader.SetSquad(squad.Id, squad.Mode);

                squad.Center = leader.transform.position;
                squad.CenterCell = WorldToCell(squad.Center);

                TryRecruit(squad, free);

                if (squad.Members.Count < MaxSquadSize && squad.GatherRadiusHex >= maxRadius)
                {
                    squad.Mode = UnitCombat.SquadMode.Sleeping;
                    squad.SleepUntil = Time.time + Mathf.Max(0.5f, SleepRetrySeconds);
                    squad.LastStateChangeTime = Time.time;
                }

                squads.Add(squad);
            }
        }

        private void TryRecruit(Squad squad, List<UnitCombat> free)
        {
            if (squad == null || free.Count == 0)
            {
                return;
            }

            var radius = Mathf.Max(1, squad.GatherRadiusHex);
            var centerCell = squad.CenterCell;
            for (int i = free.Count - 1; i >= 0 && squad.Members.Count < MaxSquadSize; i--)
            {
                var unit = free[i];
                if (unit == null || !unit.isActiveAndEnabled)
                {
                    free.RemoveAt(i);
                    continue;
                }

                if (unit.Faction != squad.Faction)
                {
                    continue;
                }

                var cell = WorldToCell(unit.transform.position);
                var distance = HexDistance(centerCell, cell);
                if (distance > radius)
                {
                    continue;
                }

                free.RemoveAt(i);
                squad.Members.Add(unit);
                unit.SetSquad(squad.Id, squad.Mode);
            }
        }

        private Vector3 ComputeCenter(List<UnitCombat> members)
        {
            var sum = Vector3.zero;
            var count = 0;
            for (int i = 0; i < members.Count; i++)
            {
                var unit = members[i];
                if (unit == null || !unit.isActiveAndEnabled)
                {
                    continue;
                }

                sum += unit.transform.position;
                count++;
            }

            if (count == 0)
            {
                return Vector3.zero;
            }

            return sum / count;
        }
    }
}
