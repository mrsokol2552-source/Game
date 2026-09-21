/*
@file: My project/Assets/Scripts/Presentation/Performance/EnemySquadManager.cs
@module: presentation.combat.squads
@purpose: Forms squads, maintains squad anchors, and switches units between macro movement and free combat states.
@entry: EnemySquadManager.Update, ESQD-03, ESQD-04, ESQD-05
@api: shared MonoBehaviour singleton driving squad-level combat orchestration
@deps: UnitCombat, FlowFieldManager, Pathfinding, faction data
@data: squads, membership, engagement distances, gather radii, anchor/target state
@perf: hotpath, impacts large-group combat stability and path request volume
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual battle verification
@config: squad sizes and threshold settings, docs/runtime_switches.md
@assets: none directly
@notes: ready/combat hysteresis is deliberate; tight thresholds cause state thrash and lost formations
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Domain.Units;
using Game.Presentation.View;
using Game.Presentation.Pathfinding;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER]
// Logical block: Scripts/Presentation/Performance/EnemySquadManager.

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Groups units into squads and drives group-centric combat movement.
    /// </summary>
    public partial class EnemySquadManager : MonoBehaviour
    {
        // [ESQD-01]
        // Enemy squad registry, anchor state, and grouping thresholds for macro combat movement.
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

        // [ESQD-02]
        // Singleton/bootstrap setup for squad management.
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

        // [ESQD-03]
        // Periodic squad rebuild and tactical state update for enemy groups.
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
