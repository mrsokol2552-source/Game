/*
@file: My project/Assets/Scripts/Presentation/View/UnitCombat.cs
@module: presentation.combat.unit
@purpose: Holds per-unit combat state, tuning, and lifecycle wiring for the combat partials.
@entry: UCOM-01, UCOM-02, UCOM-06, UCOM-08
@api: per-unit MonoBehaviour attached to combat-capable units
@deps: UnitView, PathManager, FlowFieldManager, EnemySquadManager, OrcaAvoidanceSystem, configs
@data: target state, squad membership, attack cooldowns, repath timers, combat profile data
@perf: hotpath, large-N combat update, sensitive to target scans and repath churn
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual battle verification
@config: inspector combat settings, UnitCombatProfile, UnitBehaviorProfile, docs/runtime_switches.md
@assets: unit prefabs, animation state hooks, muzzle flash / hit / death presentation
@notes: player ranged and enemy melee overrides intentionally diverge; attack range and separation must stay aligned
*/

using System.Collections.Generic;
using Game.Domain.Units;
using UnityEngine;
using Game.Presentation.Pathfinding;
using Game.Presentation.Performance;
using Game.Infrastructure.Configs;

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT]
// Logical block: Scripts/Presentation/View/UnitCombat.

namespace Game.Presentation.View
{
    [RequireComponent(typeof(UnitView))]
    [RequireComponent(typeof(SpriteRenderer))]
    public partial class UnitCombat : MonoBehaviour
    {
        // [UCOM-01]
        // Combat tuning, squad state, targeting budgets, and per-unit runtime caches.
        public static readonly HashSet<UnitCombat> All = new HashSet<UnitCombat>();
        public static bool DisableCombat = false; // Self-test or debug freeze

        public event System.Action OnAttack;
        public event System.Action OnDeath;

        public enum SquadMode
        {
            None,
            Gathering,
            Marching,
            Ready,
            FreeCombat,
            Sleeping
        }

        [Header("Combat")]
        public Faction Faction = Faction.Player;
        public float AttackRange = 1.5f;
        public int AttackDamage = 10;
        public float AttackCooldown = 0.75f;
        [Header("Per-Faction Overrides")]
        public bool UseFactionOverrides = true;
        [Tooltip("Attack range for player (ranged) units.")]
        public float PlayerAttackRange = 4.5f;
        [Tooltip("Attack range for enemy (melee) units.")]
        public float EnemyAttackRange = 1.1f;
        [Tooltip("Disable ORCA when in range (player units).")]
        public bool PlayerDisableOrcaInRange = false;
        [Tooltip("Disable ORCA when in range (enemy units).")]
        public bool EnemyDisableOrcaInRange = true;
        [Header("Profile")]
        public bool UseCombatProfile = false;
        public UnitCombatProfile CombatProfile;
        [Header("Behavior Profile")]
        public bool UseBehaviorProfile = false;
        public UnitBehaviorProfile BehaviorProfile;
        [Header("Pathfinding")]
        [Tooltip("How often (seconds) to recompute combat path when target is moving (nearby clusters).")]
        public float RepathInterval = 0.25f;
        [Tooltip("How often to recompute when target is far (cluster distance >= FarClusterDistance).")]
        public float RepathIntervalFar = 0.7f;
        [Tooltip("How often to recompute when target is very far (cluster distance >= FarClusterDistance2).")]
        public float RepathIntervalVeryFar = 1.5f;
        [Tooltip("Cooldown after a failed path attempt to avoid expensive retries each frame.")]
        public float RepathFailCooldown = 0.6f;
        [Tooltip("Cluster size (in hex cells) used to decide far/near repath intervals.")]
        public int ClusterSizeForRepath = 96;
        [Tooltip("Cluster manhattan distance threshold to use far repath interval.")]
        public int FarClusterDistance = 2;
        [Tooltip("Second level cluster size for very far repath interval.")]
        public int ClusterSizeForRepath2 = 256;
        [Tooltip("Cluster distance threshold for very far repath interval.")]
        public int FarClusterDistance2 = 1;
        [Tooltip("Restrict each path build to current cluster; if target outside, step to cluster edge first.")]
        public bool UseClusterStepping = true;
        [Tooltip("Random offset added to repath timers to desync many units.")]
        public float RepathJitter = 0.08f;
        [Tooltip("If target changes hex cell, force repath immediately (ignores timer).")]
        public bool InstantRepathOnTargetCellChange = true;
        [Tooltip("If unit is idle near enemy for longer than this, force an immediate repath to shake stall.")]
        public float StallRepathSeconds = 0.15f;
        [Header("Flow Fields")]
        [Tooltip("Use flow fields for far-distance combat movement.")]
        public bool UseFlowFields = true;
        [Tooltip("Minimum distance to target (world units) before flow field steering kicks in.")]
        public float FlowFieldMinDistance = 6f;
        [Tooltip("How often to advance to the next flow cell.")]
        public float FlowFieldStepInterval = 0.12f;
        [Tooltip("Random jitter added to flow field step interval.")]
        public float FlowFieldStepJitter = 0.04f;
        [Header("Targeting")]
        [Tooltip("How often to refresh nearest enemy search.")]
        public float TargetRefreshInterval = 0.1f;
        [Tooltip("When target is within this multiple of AttackRange, cancel combat path and stand to fight.")]
        public float EngageStopMultiplier = 1.2f;
        [Tooltip("How long to trust a job-provided nearest target before falling back to forced/local search.")]
        public float JobTargetTtl = 0.6f;
        [Tooltip("How long to keep moving toward last combat destination after losing a target.")]
        public float LostTargetGraceSeconds = 0.2f;
        [Tooltip("If a forced target exists but a nearby enemy is within AttackRange * this multiplier, prefer the local enemy.")]
        public float LocalThreatOverrideMultiplier = 3f;
        [Header("Behavior")]
        [Tooltip("If true, do not chase targets outside AttackRange.")]
        public bool HoldPosition = false;
        [Tooltip("If true, ignore non-forced targets beyond AggroRange.")]
        public bool UseAggroRange = false;
        [Tooltip("Max distance to consider non-forced targets (world units).")]
        public float AggroRange = 12f;
        [Tooltip("If true, ignore non-forced targets when far from home.")]
        public bool UseLeash = false;
        [Tooltip("Max distance from home before ignoring targets (world units).")]
        public float LeashRange = 20f;
        [Tooltip("Prefer forced targets even when a local threat exists.")]
        public bool PreferForcedTarget = false;
        [Header("Performance")]
        [Tooltip("Run combat logic no more often than this interval.")]
        public float CombatTickInterval = 0.04f;
        [Tooltip("Jitter added to combat tick to desync updates.")]
        public float CombatTickJitter = 0.02f;
        [Header("Avoidance")]
        [Tooltip("Disable ORCA velocity overrides when near attack range to allow engagement.")]
        public bool DisableOrcaWhenInRange = true;
        [Tooltip("Multiplier on AttackRange that disables ORCA (>= 1).")]
        public float OrcaDisableRangeMultiplier = 1.1f;
        [Header("Collision Spacing")]
        [Tooltip("Keep ORCA enabled when a friendly is too close (prevents overlap).")]
        public bool KeepOrcaNearFriendlies = true;
        [Tooltip("Friendly distance (world units) below which we keep ORCA enabled.")]
        public float FriendlySeparationRadius = 1.0f;
        [Header("Crouch Logic")]
        [Tooltip("Enable crouch when a friendly is directly behind this unit while attacking.")]
        public bool UseCrouchWhenBlocked = true;
        [Tooltip("How long to crouch when blocked by a friendly behind (seconds).")]
        public float CrouchBlockedSeconds = 0.45f;
        [Tooltip("Max distance behind to trigger crouch (world units).")]
        public float CrouchBehindDistance = 1.2f;
        [Tooltip("Lateral tolerance for 'behind' check (world units).")]
        public float CrouchLateralTolerance = 0.5f;
        [Header("Formation Offsets")]
        [Tooltip("Apply per-unit formation offsets near the target to reduce stacking.")]
        public bool UseFormationOffsets = true;
        [Tooltip("Start applying formation offsets within this distance to target (world units).")]
        public float FormationOffsetStartDistance = 8f;
        [Tooltip("Spacing between formation slots in hex cells.")]
        public int FormationSpacingHex = 1;
        [Tooltip("Max ring radius in hex cells for formation slots (0 = unlimited).")]
        public int FormationMaxRadiusHex = 0;
        [Header("Budgets")]
        [Tooltip("Global cap per frame to spread expensive target searches across units. 0 or less = unlimited.")]
        public static int TargetSearchBudgetPerFrame = 10;
        [Tooltip("Global cap per frame to spread expensive repaths across units. 0 or less = unlimited.")]
        public static int RepathBudgetPerFrame = 2;
        [Tooltip("Global cap per frame to spread combat ticks across units. 0 or less = unlimited.")]
        public static int CombatTickBudgetPerFrame = 48;
        [Header("Diagnostics")]
        [Tooltip("Log combat-driven path/destination resets (throttled per frame).")]
        public bool LogCombatResets = false;
        [Tooltip("Max combat reset logs per frame across all units.")]
        public int MaxCombatResetLogsPerFrame = 5;

        private float _cooldown;
        private int _currentHealth;
        private UnitView _view;
        private float _repathTimer;
        private Vector3 _lastDesired;
        private bool _combatSteering;
        private Vector2Int _lastTargetCell;
        private Game.Presentation.Pathfinding.PathManager _pm;
        private Game.Presentation.Pathfinding.HexPathfindingBootstrap _hex;
        private UnitCombat _cachedTarget;
        private float _targetRefreshTimer;
        private float _combatTickTimer;
        private UnitPathFollower _follower;
        private Transform _tr;
        private UnitCombat _forcedTarget;
        private float _forcedTargetTimer;
        private bool _pathPending;
        private float _pathPendingTimer;
        private int _pathRequestId;
        private float _stallTimer;
        private float _lostTargetGraceTimer;
        private Vector3 _lastTargetPos;
        private bool _hasLastTargetPos;
        private Vector3 _lastCombatDestination;
        private bool _hasLastCombatDestination;
        private float _flowFieldTimer;
        private bool _usingFlowField;
        private Vector3 _homePosition;
        private bool _dead;

        private static int _budgetFrame = -1;
        private static int _repathsThisFrame;
        private static int _nextCombatBudgetSlot;
        private static Game.Presentation.Pathfinding.HexPathfindingBootstrap _sharedHex;
        private UnitCombat _jobNearest;
        private float _jobNearestTimer;
        private Game.Presentation.Performance.OccupancyHash _occ;
        private UnitSpriteAnimator _anim;
        private static int _resetLogFrame = -1;
        private static int _resetLogsThisFrame;
        private static int _playerCount;
        private static int _enemyCount;
        private Game.Domain.Units.Faction _lastFaction;
        private int _combatBudgetSlot;
        [SerializeField] private int _squadId;
        [SerializeField] private SquadMode _squadMode = SquadMode.None;
        [SerializeField] private int _formationIndex = -1;

        public bool IsInSquad => _squadId != 0;
        public int SquadId => _squadId;
        public SquadMode CurrentSquadMode => _squadMode;
        public int FormationIndex => _formationIndex;
        public bool IsUsingFlowField => _usingFlowField;

        // [UCOM-02]
        // Lifecycle wiring: register the unit globally, cache collaborators, and apply profiles.
        private void OnEnable()
        {
            All.Add(this);
            RegisterFaction(Faction);
            _lastFaction = Faction;
            _combatBudgetSlot = _nextCombatBudgetSlot++;
            _tr = transform;
            _view = GetComponent<UnitView>() ?? gameObject.AddComponent<UnitView>();
            _follower = GetComponent<UnitPathFollower>();
            _anim = GetComponent<UnitSpriteAnimator>();
            _currentHealth = Mathf.Max(1, _view.Stats.MaxHealth);
            _homePosition = _tr.position;
            ApplyCombatProfile();
            ApplyFactionOverrides();
            ApplyBehaviorProfile();
            _repathTimer = Random.Range(0f, RepathJitter);
            _pm = Game.Presentation.Pathfinding.PathManager.Ensure();
            _hex = SharedHex();
            _targetRefreshTimer = Random.Range(0f, TargetRefreshInterval);
            _combatTickTimer = Random.Range(0f, CombatTickInterval + CombatTickJitter);
            _flowFieldTimer = Random.Range(0f, FlowFieldStepInterval + FlowFieldStepJitter);
            UnitCombatJobScheduler.EnsureExists();
            _occ = Game.Presentation.Performance.OccupancyHash.Instance;
        }

        private void OnDisable()
        {
            All.Remove(this);
            UnregisterFaction(_lastFaction);
            _usingFlowField = false;
            ClearSquad();
        }
    }
}
