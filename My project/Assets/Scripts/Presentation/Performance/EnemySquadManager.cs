using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Domain.Units;
using Game.Presentation.View;
using Game.Presentation.Pathfinding;

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Groups units into squads and drives group-centric combat movement.
    /// </summary>
    public class EnemySquadManager : MonoBehaviour
    {
        public static EnemySquadManager Instance { get; private set; }

        [Tooltip("How often to update squads.")]
        public float Interval = 0.2f;
        [Tooltip("Desired squad size.")]
        public int MaxSquadSize = 12;
        [Tooltip("Also manage player squads.")]
        public bool DrivePlayers = true;

        [Header("Gathering (hexes)")]
        [Tooltip("Initial search radius in hexes when forming a squad.")]
        public int InitialGatherRadiusHex = 3;
        [Tooltip("Max search radius in hexes for squad recruitment.")]
        public int MaxGatherRadiusHex = 25;
        [Tooltip("How many hexes to expand per step.")]
        public int GatherRadiusStepHex = 1;
        [Tooltip("Seconds between radius growth steps.")]
        public float GatherRadiusStepSeconds = 0.5f;
        [Tooltip("Seconds between recruitment attempts when a squad is sleeping.")]
        public float SleepRetrySeconds = 5f;

        [Header("Engagement Thresholds (hexes)")]
        [Tooltip("Distance to nearest enemy squad to enter Ready state.")]
        public int ReadyDistanceHex = 15;
        [Tooltip("Distance to nearest enemy squad to enter FreeCombat state.")]
        public int CombatDistanceHex = 13;
        [Tooltip("Distance to leave Ready state back to Marching.")]
        public int ReadyExitDistanceHex = 19;
        [Tooltip("Distance to leave FreeCombat state back to Ready.")]
        public int CombatExitDistanceHex = 17;
        [Tooltip("In FreeCombat, keep forced target until unit is within this hex distance to the enemy.")]
        public int FreeCombatReleaseHex = 0;
        [Tooltip("Minimum time before a squad can change state again.")]
        public float StateHoldSeconds = 0.5f;

        [Header("Targeting")]
        [Tooltip("How long squad target stays assigned before refresh.")]
        public float SquadTargetTTL = 1.0f;
        [Tooltip("Assign forced targets even while gathering.")]
        public bool AssignTargetsWhileGathering = true;
        [Header("Squad Flow")]
        [Tooltip("Use flow fields to advance squad center toward targets.")]
        public bool UseSquadFlow = true;
        [Tooltip("Minimum distance before squad flow applies (world units).")]
        public float SquadFlowMinDistance = 6f;

        private float _timer;
        private int _nextSquadId = 1;
        private HexPathfindingBootstrap _hex;
        private OccupancyHash _occ;

        private readonly List<UnitCombat> _freePlayers = new List<UnitCombat>(256);
        private readonly List<UnitCombat> _freeEnemies = new List<UnitCombat>(256);
        private readonly List<Squad> _playerSquads = new List<Squad>(32);
        private readonly List<Squad> _enemySquads = new List<Squad>(32);
        private readonly HashSet<int> _activeSquadIds = new HashSet<int>();
        private readonly Dictionary<int, Squad> _squadById = new Dictionary<int, Squad>(64);

        private class Squad
        {
            public int Id;
            public Faction Faction;
            public UnitCombat.SquadMode Mode = UnitCombat.SquadMode.Gathering;
            public readonly List<UnitCombat> Members = new List<UnitCombat>(12);
            public Vector3 Center;
            public Vector2Int CenterCell;
            public Vector3 MoveAnchor;
            public Vector3 TargetPos;
            public bool HasTarget;
            public int GatherRadiusHex;
            public float NextRadiusGrowTime;
            public float SleepUntil;
            public float LastStateChangeTime;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Interval;

            EnsureRefs();
            UpdateSquads(_playerSquads);
            UpdateSquads(_enemySquads);
            GatherFreeUnits();

            if (DrivePlayers)
            {
                FillSquads(_playerSquads, _freePlayers);
                CreateSquadsFromFree(_playerSquads, _freePlayers, Faction.Player);
            }
            FillSquads(_enemySquads, _freeEnemies);
            CreateSquadsFromFree(_enemySquads, _freeEnemies, Faction.Enemy);

            UpdateSquadStates(_playerSquads, _enemySquads);
            UpdateSquadStates(_enemySquads, _playerSquads);
            ApplyOrders(_playerSquads, _enemySquads);
            ApplyOrders(_enemySquads, _playerSquads);
            RebuildSquadLookup();
        }

        private void EnsureRefs()
        {
            if (_hex == null || !_hex.isActiveAndEnabled)
                _hex = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            if (_occ == null)
                _occ = OccupancyHash.Instance;
        }

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
                for (int m = squad.Members.Count - 1; m >= 0; m--)
                {
                    var uc = squad.Members[m];
                    if (uc == null || !uc.isActiveAndEnabled)
                    {
                        squad.Members.RemoveAt(m);
                        continue;
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
            for (int i = 0; i < _playerSquads.Count; i++) _activeSquadIds.Add(_playerSquads[i].Id);
            for (int i = 0; i < _enemySquads.Count; i++) _activeSquadIds.Add(_enemySquads[i].Id);

            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                if (uc.IsInSquad && !_activeSquadIds.Contains(uc.SquadId))
                {
                    uc.ClearSquad();
                }
                if (uc.IsInSquad) continue;
                if (uc.Faction == Faction.Player) _freePlayers.Add(uc);
                else if (uc.Faction == Faction.Enemy) _freeEnemies.Add(uc);
            }
        }

        private void FillSquads(List<Squad> squads, List<UnitCombat> free)
        {
            if (squads.Count == 0 || free.Count == 0) return;
            for (int i = 0; i < squads.Count && free.Count > 0; i++)
            {
                var squad = squads[i];
                if (squad == null) continue;
                if (squad.Members.Count >= MaxSquadSize) continue;

                bool isSleeping = squad.Mode == UnitCombat.SquadMode.Sleeping;
                if (isSleeping && Time.time < squad.SleepUntil) continue;
                if (isSleeping && Time.time >= squad.SleepUntil)
                {
                    squad.Mode = UnitCombat.SquadMode.Gathering;
                    squad.LastStateChangeTime = Time.time;
                }

                if (Time.time >= squad.NextRadiusGrowTime)
                {
                    int step = Mathf.Max(1, GatherRadiusStepHex);
                    int max = Mathf.Max(1, MaxGatherRadiusHex);
                    squad.GatherRadiusHex = Mathf.Min(max, squad.GatherRadiusHex + step);
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
            if (free.Count == 0) return;
            int initialRadius = Mathf.Max(1, InitialGatherRadiusHex);
            int maxRadius = Mathf.Max(initialRadius, MaxGatherRadiusHex);

            while (free.Count > 0)
            {
                var leader = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
                if (leader == null || !leader.isActiveAndEnabled) continue;

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
            if (squad == null || free.Count == 0) return;
            int radius = Mathf.Max(1, squad.GatherRadiusHex);
            var centerCell = squad.CenterCell;
            for (int i = free.Count - 1; i >= 0 && squad.Members.Count < MaxSquadSize; i--)
            {
                var uc = free[i];
                if (uc == null || !uc.isActiveAndEnabled) { free.RemoveAt(i); continue; }
                if (uc.Faction != squad.Faction) continue;
                var cell = WorldToCell(uc.transform.position);
                int dist = HexDistance(centerCell, cell);
                if (dist <= radius)
                {
                    free.RemoveAt(i);
                    squad.Members.Add(uc);
                    uc.SetSquad(squad.Id, squad.Mode);
                }
            }
        }

        private void UpdateSquadStates(List<Squad> squads, List<Squad> enemies)
        {
            if (squads == null || squads.Count == 0) return;
            int ready = Mathf.Max(1, ReadyDistanceHex);
            int combat = Mathf.Max(1, CombatDistanceHex);
            int readyExit = Mathf.Max(ready + 1, ReadyExitDistanceHex);
            int combatExit = Mathf.Max(combat + 1, CombatExitDistanceHex);

            for (int i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || squad.Members.Count == 0) continue;
                int enemyDist = FindNearestEnemySquadDistanceHex(squad, enemies);
                var current = squad.Mode;
                var desired = current;

                if (enemyDist == int.MaxValue)
                {
                    if (current == UnitCombat.SquadMode.FreeCombat || current == UnitCombat.SquadMode.Ready)
                        desired = UnitCombat.SquadMode.Marching;
                    else if (current == UnitCombat.SquadMode.Gathering)
                        desired = UnitCombat.SquadMode.Gathering;
                    else if (current == UnitCombat.SquadMode.Sleeping)
                        desired = UnitCombat.SquadMode.Sleeping;
                    else
                        desired = UnitCombat.SquadMode.Marching;
                }
                else
                {
                    switch (current)
                    {
                        case UnitCombat.SquadMode.FreeCombat:
                            if (enemyDist >= combatExit)
                                desired = UnitCombat.SquadMode.Ready;
                            break;
                        case UnitCombat.SquadMode.Ready:
                            if (enemyDist <= combat)
                                desired = UnitCombat.SquadMode.FreeCombat;
                            else if (enemyDist >= readyExit)
                                desired = UnitCombat.SquadMode.Marching;
                            break;
                        case UnitCombat.SquadMode.Sleeping:
                            if (enemyDist <= combat)
                                desired = UnitCombat.SquadMode.FreeCombat;
                            else if (enemyDist <= ready)
                                desired = UnitCombat.SquadMode.Ready;
                            else
                                desired = UnitCombat.SquadMode.Sleeping;
                            break;
                        default:
                            if (enemyDist <= combat)
                                desired = UnitCombat.SquadMode.FreeCombat;
                            else if (enemyDist <= ready)
                                desired = UnitCombat.SquadMode.Ready;
                            else
                                desired = UnitCombat.SquadMode.Marching;
                            break;
                    }
                }

                if (desired != current)
                {
                    float hold = Mathf.Max(0f, StateHoldSeconds);
                    if (Time.time - squad.LastStateChangeTime >= hold)
                    {
                        squad.Mode = desired;
                        squad.LastStateChangeTime = Time.time;
                    }
                }
            }
        }

        private void ApplyOrders(List<Squad> squads, List<Squad> enemies)
        {
            if (squads == null || squads.Count == 0) return;
            float ttl = Mathf.Max(SquadTargetTTL, Interval * 2f);
            for (int i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || squad.Members.Count == 0) continue;
                bool isFreeCombat = squad.Mode == UnitCombat.SquadMode.FreeCombat;
                bool assignTargets = !isFreeCombat && (AssignTargetsWhileGathering || squad.Mode != UnitCombat.SquadMode.Gathering);
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

                for (int m = squad.Members.Count - 1; m >= 0; m--)
                {
                    var uc = squad.Members[m];
                    if (uc == null || !uc.isActiveAndEnabled)
                    {
                        squad.Members.RemoveAt(m);
                        continue;
                    }
                    if (!uc.IsInSquad || uc.SquadId != squad.Id)
                        uc.SetSquad(squad.Id, squad.Mode);
                    else
                        uc.SetSquadMode(squad.Mode);
                    uc.SetFormationIndex(m);

                    if (assignTargets && target != null)
                        uc.AssignSquadTarget(target, ttl);
                    else if (isFreeCombat)
                    {
                        if (target == null)
                        {
                            uc.ClearForcedTarget();
                            continue;
                        }

                        float releaseWorld = 0f;
                        if (_hex != null && FreeCombatReleaseHex > 0)
                        {
                            releaseWorld = FreeCombatReleaseHex * _hex.HexSize * 0.75f;
                        }
                        float releaseByRange = Mathf.Max(0f, uc.AttackRange * 2f);
                        float releaseDist = releaseWorld > 0f ? releaseWorld : releaseByRange;
                        if (releaseDist <= 0f)
                        {
                            uc.AssignSquadTarget(target, ttl);
                            continue;
                        }

                        float distWorld = (uc.transform.position - target.transform.position).magnitude;
                        if (distWorld > releaseDist)
                            uc.AssignSquadTarget(target, ttl);
                        else
                            uc.ClearForcedTarget();
                    }
                }
            }
        }

        private UnitCombat FindNearestEnemyUnit(Vector3 from, Faction faction, List<Squad> enemySquads)
        {
            if (_occ != null && _occ.TryGetNearestEnemy(from, faction, out var enemy))
                return enemy;

            UnitCombat best = null;
            float best2 = float.MaxValue;
            for (int i = 0; i < enemySquads.Count; i++)
            {
                var squad = enemySquads[i];
                if (squad == null) continue;
                for (int m = 0; m < squad.Members.Count; m++)
                {
                    var uc = squad.Members[m];
                    if (uc == null || !uc.isActiveAndEnabled) continue;
                    float d2 = (uc.transform.position - from).sqrMagnitude;
                    if (d2 < best2)
                    {
                        best2 = d2;
                        best = uc;
                    }
                }
            }
            return best;
        }

        private int FindNearestEnemySquadDistanceHex(Squad squad, List<Squad> enemies)
        {
            if (squad == null || enemies == null || enemies.Count == 0) return int.MaxValue;
            int best = int.MaxValue;
            for (int i = 0; i < enemies.Count; i++)
            {
                var other = enemies[i];
                if (other == null || other.Members.Count == 0) continue;
                int dist = HexDistance(squad.CenterCell, other.CenterCell);
                if (dist < best)
                    best = dist;
            }
            return best;
        }

        private Vector3 ComputeCenter(List<UnitCombat> members)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < members.Count; i++)
            {
                var uc = members[i];
                if (uc == null || !uc.isActiveAndEnabled) continue;
                sum += uc.transform.position;
                count++;
            }
            if (count == 0) return Vector3.zero;
            return sum / count;
        }

        private Vector3 ComputeSquadAnchor(Squad squad, Vector3 targetPos)
        {
            if (squad == null) return targetPos;
            if (!UseSquadFlow) return targetPos;
            var flow = FlowFieldManager.Instance;
            if (flow == null || !flow.Enabled) return targetPos;
            float dist = (targetPos - squad.Center).magnitude;
            if (dist <= Mathf.Max(0.1f, SquadFlowMinDistance)) return targetPos;
            if (flow.TryGetNextPoint(squad.Center, targetPos, squad.Faction, out var next))
                return next;
            return targetPos;
        }

        private void RebuildSquadLookup()
        {
            _squadById.Clear();
            for (int i = 0; i < _playerSquads.Count; i++)
            {
                var squad = _playerSquads[i];
                if (squad == null) continue;
                _squadById[squad.Id] = squad;
            }
            for (int i = 0; i < _enemySquads.Count; i++)
            {
                var squad = _enemySquads[i];
                if (squad == null) continue;
                _squadById[squad.Id] = squad;
            }
        }

        public bool TryGetSquadAnchor(int squadId, out Vector3 anchor, out Vector3 targetPos, out UnitCombat.SquadMode mode)
        {
            anchor = default;
            targetPos = default;
            mode = UnitCombat.SquadMode.None;
            if (squadId == 0) return false;
            if (_squadById.TryGetValue(squadId, out var squad) && squad != null)
            {
                anchor = squad.MoveAnchor;
                targetPos = squad.TargetPos;
                mode = squad.Mode;
                return true;
            }
            return false;
        }

        private Vector2Int WorldToCell(Vector3 world)
        {
            if (_hex == null) return Vector2Int.zero;
            return _hex.WorldToGrid(world);
        }

        private static int HexDistance(Vector2Int a, Vector2Int b)
        {
            int aq = a.x - (a.y - (a.y & 1)) / 2;
            int ar = a.y;
            int bq = b.x - (b.y - (b.y & 1)) / 2;
            int br = b.y;
            int dq = aq - bq;
            int dr = ar - br;
            int ds = (aq + ar) - (bq + br);
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("EnemySquadManager");
            go.AddComponent<EnemySquadManager>();
        }
    }
}
