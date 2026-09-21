/*
@file: My project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Tactics.cs
@module: presentation.combat.squads
@purpose: Extracted squad-state transitions, target assignment, and flow-anchor updates for EnemySquadManager.
@entry: ESQD-05
@api: EnemySquadManager partial tactical state/order helpers
@deps: UnitCombat targeting, FlowFieldManager, OccupancyHash
@data: squad mode transitions, target ttl, move anchors, nearest-enemy lookups
@perf: hotpath during active engagements and squad-to-squad transitions
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual battle verification
@config: Ready/Combat thresholds, SquadTargetTTL, UseSquadFlow, SquadFlowMinDistance
@assets: none
@notes: hysteresis and forced-target release rules should stay coupled to avoid mode thrash
*/

using System.Collections.Generic;
using UnityEngine;
using Game.Domain.Units;
using Game.Presentation.Pathfinding;
using Game.Presentation.View;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER-TACTICS]
// Logical block: EnemySquadManager tactical state/order extraction.

namespace Game.Presentation.Performance
{
    public partial class EnemySquadManager
    {
        // [ESQD-05]
        // State transitions between gathering, marching, ready, free-combat, and sleep modes.
        private void UpdateSquadStates(List<Squad> squads, List<Squad> enemies)
        {
            if (squads == null || squads.Count == 0)
            {
                return;
            }

            var readyDistance = Mathf.Max(1, ReadyDistanceHex);
            var combatDistance = Mathf.Max(1, CombatDistanceHex);
            var readyExitDistance = Mathf.Max(readyDistance + 1, ReadyExitDistanceHex);
            var combatExitDistance = Mathf.Max(combatDistance + 1, CombatExitDistanceHex);

            for (int i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || squad.Members.Count == 0)
                {
                    continue;
                }

                var enemyDistance = FindNearestEnemySquadDistanceHex(squad, enemies);
                var current = squad.Mode;
                var desired = current;

                if (enemyDistance == int.MaxValue)
                {
                    desired = current switch
                    {
                        UnitCombat.SquadMode.FreeCombat => UnitCombat.SquadMode.Marching,
                        UnitCombat.SquadMode.Ready => UnitCombat.SquadMode.Marching,
                        UnitCombat.SquadMode.Gathering => UnitCombat.SquadMode.Gathering,
                        UnitCombat.SquadMode.Sleeping => UnitCombat.SquadMode.Sleeping,
                        _ => UnitCombat.SquadMode.Marching
                    };
                }
                else
                {
                    switch (current)
                    {
                        case UnitCombat.SquadMode.FreeCombat:
                            if (enemyDistance >= combatExitDistance)
                            {
                                desired = UnitCombat.SquadMode.Ready;
                            }
                            break;

                        case UnitCombat.SquadMode.Ready:
                            if (enemyDistance <= combatDistance)
                            {
                                desired = UnitCombat.SquadMode.FreeCombat;
                            }
                            else if (enemyDistance >= readyExitDistance)
                            {
                                desired = UnitCombat.SquadMode.Marching;
                            }
                            break;

                        case UnitCombat.SquadMode.Sleeping:
                            if (enemyDistance <= combatDistance)
                            {
                                desired = UnitCombat.SquadMode.FreeCombat;
                            }
                            else if (enemyDistance <= readyDistance)
                            {
                                desired = UnitCombat.SquadMode.Ready;
                            }
                            break;

                        default:
                            if (enemyDistance <= combatDistance)
                            {
                                desired = UnitCombat.SquadMode.FreeCombat;
                            }
                            else if (enemyDistance <= readyDistance)
                            {
                                desired = UnitCombat.SquadMode.Ready;
                            }
                            else
                            {
                                desired = UnitCombat.SquadMode.Marching;
                            }
                            break;
                    }
                }

                if (desired == current)
                {
                    continue;
                }

                var hold = Mathf.Max(0f, StateHoldSeconds);
                if (Time.time - squad.LastStateChangeTime < hold)
                {
                    continue;
                }

                squad.Mode = desired;
                squad.LastStateChangeTime = Time.time;
            }
        }

        private void ApplyOrders(List<Squad> squads, List<Squad> enemies)
        {
            if (squads == null || squads.Count == 0)
            {
                return;
            }

            var ttl = Mathf.Max(SquadTargetTTL, Interval * 2f);
            for (int i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || squad.Members.Count == 0)
                {
                    continue;
                }

                var isFreeCombat = squad.Mode == UnitCombat.SquadMode.FreeCombat;
                var assignTargets = !isFreeCombat && (AssignTargetsWhileGathering || squad.Mode != UnitCombat.SquadMode.Gathering);
                UnitCombat target = null;
                if (assignTargets || isFreeCombat)
                {
                    target = FindNearestEnemyUnit(squad.Center, squad.Faction, enemies);
                }

                if (target != null)
                {
                    squad.HasTarget = true;
                    squad.TargetPos = target.transform.position;
                    squad.MoveAnchor = ComputeSquadAnchor(squad, squad.TargetPos);
                }
                else
                {
                    squad.HasTarget = false;
                    squad.TargetPos = squad.Center;
                    squad.MoveAnchor = squad.Center;
                }

                for (int memberIndex = squad.Members.Count - 1; memberIndex >= 0; memberIndex--)
                {
                    var unit = squad.Members[memberIndex];
                    if (unit == null || !unit.isActiveAndEnabled)
                    {
                        squad.Members.RemoveAt(memberIndex);
                        continue;
                    }

                    if (!unit.IsInSquad || unit.SquadId != squad.Id)
                    {
                        unit.SetSquad(squad.Id, squad.Mode);
                    }
                    else
                    {
                        unit.SetSquadMode(squad.Mode);
                    }

                    unit.SetFormationIndex(memberIndex);

                    if (assignTargets && target != null)
                    {
                        unit.AssignSquadTarget(target, ttl);
                        continue;
                    }

                    if (!isFreeCombat)
                    {
                        continue;
                    }

                    if (target == null)
                    {
                        unit.ClearForcedTarget();
                        continue;
                    }

                    var releaseWorld = 0f;
                    if (_hex != null && FreeCombatReleaseHex > 0)
                    {
                        releaseWorld = FreeCombatReleaseHex * _hex.HexSize * 0.75f;
                    }

                    var releaseByRange = Mathf.Max(0f, unit.AttackRange * 2f);
                    var releaseDistance = releaseWorld > 0f ? releaseWorld : releaseByRange;
                    if (releaseDistance <= 0f)
                    {
                        unit.AssignSquadTarget(target, ttl);
                        continue;
                    }

                    var distance = (unit.transform.position - target.transform.position).magnitude;
                    if (distance > releaseDistance)
                    {
                        unit.AssignSquadTarget(target, ttl);
                    }
                    else
                    {
                        unit.ClearForcedTarget();
                    }
                }
            }
        }

        private UnitCombat FindNearestEnemyUnit(Vector3 from, Faction faction, List<Squad> enemySquads)
        {
            if (_occ != null && _occ.TryGetNearestEnemy(from, faction, out var enemy))
            {
                return enemy;
            }

            UnitCombat best = null;
            var bestDistanceSquared = float.MaxValue;
            for (int i = 0; i < enemySquads.Count; i++)
            {
                var squad = enemySquads[i];
                if (squad == null)
                {
                    continue;
                }

                for (int memberIndex = 0; memberIndex < squad.Members.Count; memberIndex++)
                {
                    var unit = squad.Members[memberIndex];
                    if (unit == null || !unit.isActiveAndEnabled)
                    {
                        continue;
                    }

                    var distanceSquared = (unit.transform.position - from).sqrMagnitude;
                    if (distanceSquared >= bestDistanceSquared)
                    {
                        continue;
                    }

                    bestDistanceSquared = distanceSquared;
                    best = unit;
                }
            }

            return best;
        }

        private int FindNearestEnemySquadDistanceHex(Squad squad, List<Squad> enemies)
        {
            if (squad == null || enemies == null || enemies.Count == 0)
            {
                return int.MaxValue;
            }

            var best = int.MaxValue;
            for (int i = 0; i < enemies.Count; i++)
            {
                var other = enemies[i];
                if (other == null || other.Members.Count == 0)
                {
                    continue;
                }

                var distance = HexDistance(squad.CenterCell, other.CenterCell);
                if (distance < best)
                {
                    best = distance;
                }
            }

            return best;
        }

        private Vector3 ComputeSquadAnchor(Squad squad, Vector3 targetPos)
        {
            if (squad == null || !UseSquadFlow)
            {
                return targetPos;
            }

            var flow = FlowFieldManager.Instance;
            if (flow == null || !flow.Enabled)
            {
                return targetPos;
            }

            var distance = (targetPos - squad.Center).magnitude;
            if (distance <= Mathf.Max(0.1f, SquadFlowMinDistance))
            {
                return targetPos;
            }

            if (flow.TryGetNextPoint(squad.Center, targetPos, squad.Faction, out var next))
            {
                return next;
            }

            return targetPos;
        }

        private void RebuildSquadLookup()
        {
            _squadById.Clear();
            for (int i = 0; i < _playerSquads.Count; i++)
            {
                var squad = _playerSquads[i];
                if (squad != null)
                {
                    _squadById[squad.Id] = squad;
                }
            }

            for (int i = 0; i < _enemySquads.Count; i++)
            {
                var squad = _enemySquads[i];
                if (squad != null)
                {
                    _squadById[squad.Id] = squad;
                }
            }
        }
    }
}
