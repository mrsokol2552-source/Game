using System.Collections.Generic;
using Game.Domain.Units;
using UnityEngine;
using Game.Presentation.Pathfinding;
using Game.Presentation.Performance;
using System.Linq;
using Game.Infrastructure.Configs;

namespace Game.Presentation.View
{
    [RequireComponent(typeof(UnitView))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class UnitCombat : MonoBehaviour
    {
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
        private float _flowFieldTimer;
        private bool _usingFlowField;
        private Vector3 _homePosition;
        private bool _dead;

        private static int _budgetFrame = -1;
        private static int _repathsThisFrame;
        private static Game.Presentation.Pathfinding.HexPathfindingBootstrap _sharedHex;
        private UnitCombat _jobNearest;
        private float _jobNearestTimer;
        private Game.Presentation.Performance.OccupancyHash _occ;
        private static int _resetLogFrame = -1;
        private static int _resetLogsThisFrame;
        private static int _playerCount;
        private static int _enemyCount;
        private Game.Domain.Units.Faction _lastFaction;
        [SerializeField] private int _squadId;
        [SerializeField] private SquadMode _squadMode = SquadMode.None;
        [SerializeField] private int _formationIndex = -1;

        public bool IsInSquad => _squadId != 0;
        public int SquadId => _squadId;
        public SquadMode CurrentSquadMode => _squadMode;
        public int FormationIndex => _formationIndex;
        public bool IsUsingFlowField => _usingFlowField;

        private void OnEnable()
        {
            All.Add(this);
            RegisterFaction(Faction);
            _lastFaction = Faction;
            _tr = transform;
            _view = GetComponent<UnitView>() ?? gameObject.AddComponent<UnitView>();
            _follower = GetComponent<UnitPathFollower>();
            _currentHealth = Mathf.Max(1, _view.Stats.MaxHealth);
            _homePosition = _tr.position;
            ApplyCombatProfile();
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

        private void Update()
        {
            _combatTickTimer -= Time.deltaTime;
            if (_combatTickTimer > 0f) return;
            _combatTickTimer = CombatTickInterval + Random.Range(0f, CombatTickJitter);

            if (DisableCombat) return;
            if (Faction != _lastFaction)
            {
                UnregisterFaction(_lastFaction);
                RegisterFaction(Faction);
                _lastFaction = Faction;
            }
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - (CombatTickInterval));
            if (_forcedTargetTimer > 0f)
                _forcedTargetTimer -= Time.deltaTime;
            else
                _forcedTarget = null;
            if (_jobNearestTimer > 0f)
                _jobNearestTimer -= Time.deltaTime;
            else
                _jobNearest = null;
            if (_forcedTarget != null && (!_forcedTarget.isActiveAndEnabled || _forcedTarget.Faction == Faction))
            {
                _forcedTarget = null;
                _forcedTargetTimer = 0f;
            }
            if (_jobNearest != null && (!_jobNearest.isActiveAndEnabled || _jobNearest.Faction == Faction))
            {
                _jobNearest = null;
                _jobNearestTimer = 0f;
            }
            if (_occ == null) _occ = Game.Presentation.Performance.OccupancyHash.Instance;

            EnsureCaches();
            bool pathActive = _follower != null && _follower.HasPath;
            bool allowIndividualPaths = _squadMode == SquadMode.None || _squadMode == SquadMode.FreeCombat;
            bool allowPerUnitPaths = _squadMode == SquadMode.None;
            if (!allowPerUnitPaths && _pathPending)
                InvalidatePendingPath();
            if (_pathPendingTimer > 0f)
            {
                _pathPendingTimer -= Time.deltaTime;
                if (_pathPendingTimer <= 0f)
                {
                    _pathPending = false;
                }
            }

            // If no enemies exist, stop combat movement and keep manual commands.
            bool enemiesPresent = HasEnemyForFaction(Faction);
            if (!enemiesPresent && _forcedTarget == null)
            {
                if (_follower != null && _follower.Source == UnitPathFollower.PathSource.Combat) _follower.Cancel();
                if (_combatSteering) _view.ClearDestination("combat-no-enemies");
                _combatSteering = false;
                _pathPending = false;
                _lostTargetGraceTimer = 0f;
                InvalidatePendingPath();
                if (_view != null && DisableOrcaWhenInRange)
                    _view.UseOrcaVelocity = true;
                return;
            }

            var target = ResolveTarget();
            if (target != null)
            {
                bool isForced = _forcedTarget != null && target == _forcedTarget;
                float distCheck = (_tr.position - target.transform.position).magnitude;
                if (ShouldIgnoreTarget(distCheck, isForced))
                {
                    _cachedTarget = null;
                    target = null;
                }
            }
            if (target != null)
            {
                _lostTargetGraceTimer = LostTargetGraceSeconds;
                _lastTargetPos = target.transform.position;
                _hasLastTargetPos = true;
                Vector3 tp = target.transform.position;
                Vector3 mp = _tr.position;
                if (_hex == null) _hex = SharedHex();
                var targetCell = _hex != null ? _hex.WorldToGrid(tp) : Vector2Int.zero;
                float dist = (tp - mp).magnitude;
                float stopDist = Mathf.Max(AttackRange * 0.9f, 0.1f);
                if (_view != null && DisableOrcaWhenInRange)
                {
                    float orcaDisableDist = AttackRange * Mathf.Max(1f, OrcaDisableRangeMultiplier);
                    _view.UseOrcaVelocity = dist > orcaDisableDist;
                }

                // If path follower is active, don't override its movement
                float cancelDist = Mathf.Max(stopDist, AttackRange * 0.95f);
                if (dist <= cancelDist && pathActive)
                {
                    if (_follower != null && _follower.Source == UnitPathFollower.PathSource.Combat) _follower.Cancel();
                    _view.ClearDestination("combat-in-range");
                    LogReset("in-range-stop", target, dist);
                    pathActive = false;
                    _pathPending = false;
                    InvalidatePendingPath();
                }

                if (dist > AttackRange)
                {
                    if (HoldPosition)
                    {
                        if (pathActive && _follower != null && _follower.Source == UnitPathFollower.PathSource.Combat)
                            _follower.Cancel();
                        if (_combatSteering)
                            _view.ClearDestination("combat-hold-position");
                        _combatSteering = false;
                        _usingFlowField = false;
                        goto SkipBuild;
                    }
                    // Move to a point at stopDist from the target
                    Vector3 dirToTarget = (tp - mp);
                    if (dirToTarget.sqrMagnitude < 0.0001f) dirToTarget = Vector3.right;
                    Vector3 desired = tp - dirToTarget.normalized * stopDist;
                    bool useSquadAnchor = false;
                    var squadMgr = EnemySquadManager.Instance;
                    if (IsInSquad && _squadMode != SquadMode.FreeCombat && squadMgr != null)
                    {
                        if (squadMgr.TryGetSquadAnchor(_squadId, out var anchor, out _, out var squadMode) &&
                            squadMode != SquadMode.FreeCombat)
                        {
                            desired = anchor;
                            useSquadAnchor = true;
                        }
                    }
                    bool formationApplied = false;
                    if (UseFormationOffsets && _formationIndex >= 0 && IsInSquad && _squadMode != SquadMode.FreeCombat &&
                        (useSquadAnchor || dist <= FormationOffsetStartDistance))
                    {
                        if (_hex == null) _hex = SharedHex();
                        if (_hex != null)
                        {
                            var anchorCell = _hex.WorldToGrid(desired);
                            var anchorAxial = OddRToAxial(anchorCell);
                            var offsetAxial = FormationAxialOffset(_formationIndex, Mathf.Max(1, FormationSpacingHex), FormationMaxRadiusHex);
                            var targetAxial = new Axial(anchorAxial.q + offsetAxial.q, anchorAxial.r + offsetAxial.r);
                            var offsetCell = AxialToOddR(targetAxial);
                            offsetCell.x = Mathf.Clamp(offsetCell.x, 0, _hex.Width - 1);
                            offsetCell.y = Mathf.Clamp(offsetCell.y, 0, _hex.Height - 1);
                            desired = _hex.GridToWorld(offsetCell.x, offsetCell.y);
                            formationApplied = true;
                        }
                    }
                    if (!formationApplied)
                    {
                        // small per-unit jitter perpendicular to target direction to avoid piling into same hex
                        float jitterScale = dist <= AttackRange * 2f ? 0.2f : 0.35f;
                        float jitter = AttackRange * jitterScale;
                        int hash = GetInstanceID();
                        float r = (Mathf.Sin(hash * 12.9898f) * 43758.5453f) % 1f; // deterministic 0..1
                        Vector3 perp = new Vector3(-dirToTarget.normalized.y, dirToTarget.normalized.x, 0f);
                        desired += perp * ((r - 0.5f) * jitter);
                    }
                    // Snap to hex center only when moving into a different cell to avoid "return to center" jitter.
                    if (_hex == null) _hex = SharedHex();
                    if (_hex != null)
                    {
                        var desiredCell = _hex.WorldToGrid(desired);
                        var currentCell = _hex.WorldToGrid(mp);
                        if (desiredCell != currentCell)
                            desired = _hex.GridToWorld(desiredCell.x, desiredCell.y);
                    }
                    // Avoid targeting an already occupied enemy cell; friendlies will be handled by culling/resolver
                    if (_pm != null && _pm.IsWorldOccupied(desired, _view, enemiesOnly: true) && _pm.TryFindNearestFreeWorld(desired, _view, 2, out var free))
                    {
                        desired = free;
                    }

                    // Pathfinding is always enabled for combat steering
                    bool forceFlowField = !allowIndividualPaths;
                    if (TryFlowFieldMove(desired, dist, pathActive, forceFlowField))
                    {
                        goto SkipBuild;
                    }
                    if (!allowIndividualPaths)
                    {
                        InvalidatePendingPath();
                        if (pathActive && _follower != null && _follower.Source == UnitPathFollower.PathSource.Combat)
                            _follower.Cancel();
                        _view.SetDestination(desired);
                        _combatSteering = true;
                        goto SkipBuild;
                    }
                    if (_repathTimer > 0f) _repathTimer -= Time.deltaTime;
                    float delta2 = (desired - _lastDesired).sqrMagnitude;
                    bool targetMovedCell = _hex != null && targetCell != _lastTargetCell;
                    if (InstantRepathOnTargetCellChange && targetMovedCell && !_pathPending)
                    {
                        _repathTimer = 0f;
                    }
                    // If we are in range band but idle, nudge toward desired to close gap
                    if (!pathActive && !_pathPending && dist > AttackRange * 0.9f)
                    {
                        _view.SetDestination(desired);
                        _combatSteering = true;
                    }
                    if (allowPerUnitPaths && _repathTimer <= 0f && (!_pathPending || _pathPendingTimer <= 0f) && (!pathActive || targetMovedCell || delta2 > 0.05f * 0.05f))
                    {
                        _usingFlowField = false;
                        if (!TryConsumeRepathBudget())
                        {
                            _repathTimer = 0.05f + Random.Range(0f, RepathJitter);
                            goto SkipBuild;
                        }
                        if (_pm != null)
                        {
                            int cd2 = _pm.ClusterDistance(mp, tp, ClusterSizeForRepath2);
                            if (cd2 >= FarClusterDistance2 + 2 && !pathActive)
                            {
                                _repathTimer = RepathIntervalVeryFar + Random.Range(0f, RepathJitter);
                                _combatSteering = true;
                                _lastTargetCell = targetCell;
                                // skip building a path this tick
                                goto SkipBuild;
                            }
                        }
                            if (_pm != null && _pm.IsWorldOccupied(desired, _view) && _pm.TryFindNearestFreeWorld(desired, _view, 2, out var alt))
                                desired = alt;
                            Vector3 buildTarget = desired;
                            if (UseClusterStepping && _pm != null && _pm.ClusterDistance(mp, tp, ClusterSizeForRepath) > 0)
                            {
                                if (_pm.TryGetClusterEdgeTarget(mp, tp, ClusterSizeForRepath, _view, out var edge))
                                    buildTarget = edge;
                            }
                            Game.Presentation.Pathfinding.PathRequestQueue.Ensure();
                            bool pathActiveLocal = pathActive;
                            var desiredLocal = desired;
                            var targetRef = target;
                            _pathPending = true;
                            _pathPendingTimer = 0.35f;
                            int reqId = ++_pathRequestId;
                            Game.Presentation.Pathfinding.PathRequestQueue.Instance.Enqueue(_view, buildTarget, allowDiag: true, smooth: true, onDone: (built, worldPath) =>
                            {
                                if (this == null || !isActiveAndEnabled || _view == null)
                                {
                                    if (worldPath != null) Game.Presentation.Pathfinding.PathManager.ReturnWorldList(worldPath);
                                    return;
                                }
                                if (reqId != _pathRequestId)
                                {
                                    if (worldPath != null) Game.Presentation.Pathfinding.PathManager.ReturnWorldList(worldPath);
                                    return;
                                }
                                _pathPending = false;
                                // Drop stale results if target has changed
                                if (_cachedTarget != targetRef || targetRef == null)
                                {
                                    if (worldPath != null) Game.Presentation.Pathfinding.PathManager.ReturnWorldList(worldPath);
                                    return;
                                }
                                if (!built && buildTarget != desiredLocal && _pm != null)
                                {
                                    built = _pm.BuildPath(_view, desiredLocal, allowDiag: true, smooth: true, autoFit: false, out worldPath, blockFriendlies: false);
                                }
                                if (built)
                                {
                                    _usingFlowField = false;
                                    if (_follower == null) _follower = gameObject.AddComponent<UnitPathFollower>();
                                    _follower.SetWorldPath(worldPath, UnitPathFollower.PathSource.Combat);
                                    Game.Presentation.Pathfinding.PathManager.ReturnWorldList(worldPath);
                                    _lastDesired = desiredLocal;
                                    float interval = RepathInterval;
                                    if (_pm != null)
                                    {
                                        int cd1 = _pm.ClusterDistance(mp, tp, ClusterSizeForRepath);
                                        int cd2 = _pm.ClusterDistance(mp, tp, ClusterSizeForRepath2);
                                        if (cd2 >= FarClusterDistance2)
                                            interval = RepathIntervalVeryFar;
                                        else if (cd1 >= FarClusterDistance)
                                            interval = RepathIntervalFar;
                                        // adapt interval by distance buckets
                                        float distHex = _hex != null ? (mp - tp).magnitude / (_hex.HexSize * 0.75f) : 0f;
                                        if (distHex > 100f) interval *= 3f;
                                        else if (distHex > 50f) interval *= 2f;
                                    }
                                    _repathTimer = interval + Random.Range(0f, RepathJitter);
                                    _combatSteering = true;
                                    _lastTargetCell = targetCell;
                                }
                                else
                                {
                                    _repathTimer = RepathFailCooldown + Random.Range(0f, RepathJitter);
                                    bool allowed = true;
                                    if (_hex != null && !_hex.IsWalkableWorld(desiredLocal))
                                        allowed = false;
                                    if (_pm != null && _pm.IsWorldOccupied(desiredLocal, _view))
                                        allowed = false;
                                    if (!pathActiveLocal && allowed)
                                    {
                                        _usingFlowField = false;
                                        _view.SetDestination(desiredLocal);
                                        _combatSteering = true;
                                    }
                                }
                            });
                        }
                SkipBuild:
                    ;
                }
                else
                {
                    // In range: attack on cooldown; avoid clearing destination if path is active
                    if (pathActive && _follower != null && _follower.Source == UnitPathFollower.PathSource.Combat)
                    {
                        _follower.Cancel();
                        pathActive = false;
                        _pathPending = false;
                    }
                    _usingFlowField = false;
                    if (!pathActive && _combatSteering)
                        _view.ClearDestination("combat-in-range");
                    if (_cooldown <= 0f)
                    {
                        OnAttack?.Invoke();
                        target.ApplyDamage(AttackDamage);
                        _cooldown = AttackCooldown;
                    }
                }
            }
            else
            {
                if (_view != null && DisableOrcaWhenInRange)
                    _view.UseOrcaVelocity = true;
                if (enemiesPresent && _lostTargetGraceTimer > 0f)
                {
                    _lostTargetGraceTimer = Mathf.Max(0f, _lostTargetGraceTimer - Time.deltaTime);
                    if (_hasLastTargetPos && _view != null && _view.TryGetDestination(out var dest))
                    {
                        var pos = _tr.position;
                        var toDest = dest - pos; toDest.z = 0f;
                        var toLast = _lastTargetPos - pos; toLast.z = 0f;
                        if (toDest.sqrMagnitude > 0.0001f && toLast.sqrMagnitude > 0.0001f)
                        {
                            if (Vector3.Dot(toDest.normalized, toLast.normalized) < 0f)
                            {
                                if (pathActive && _follower != null && _follower.Source == UnitPathFollower.PathSource.Combat)
                                    _follower.Cancel();
                                if (_combatSteering)
                                    _view.ClearDestination("combat-lost-target");
                                _combatSteering = false;
                                _pathPending = false;
                                _lostTargetGraceTimer = 0f;
                            }
                        }
                    }
                    _stallTimer = 0f;
                }
                else
                {
                    _lostTargetGraceTimer = 0f;
                    // No targets left: only cancel combat-driven movement, keep player's commands/path
                    if (pathActive && _follower.Source == UnitPathFollower.PathSource.Combat) _follower.Cancel();
                    if (_combatSteering)
                    {
                        _view.ClearDestination("combat-no-enemies");
                        LogReset("no-enemies", null, 0f);
                        // Snap back to hex center to avoid drifting offsets after combat
                        if (_hex == null) _hex = SharedHex();
                        if (_hex != null)
                        {
                            var cell = _hex.WorldToGrid(_tr.position);
                            _tr.position = _hex.GridToWorld(cell.x, cell.y);
                        }
                    }
                    _combatSteering = false;
                    _usingFlowField = false;
                    _stallTimer = 0f;
                }
            }

            // Stall breaker: if we are near an enemy but not moving/pathing, force a repath soon
            if (target != null)
            {
                bool movingOrPending = pathActive || _pathPending || (_view != null && _view.TryGetDestination(out _));
                if (!movingOrPending && target.isActiveAndEnabled)
                {
                    float dist = (target.transform.position - _tr.position).magnitude;
                    if (dist > AttackRange * 0.9f)
                    {
                        _stallTimer += Time.deltaTime;
                        if (_stallTimer >= StallRepathSeconds)
                        {
                            _repathTimer = 0f;
                            _pathPending = false;
                            _stallTimer = 0f;
                        }
                    }
                    else
                    {
                        _stallTimer = 0f;
                    }
                }
                else
                {
                    _stallTimer = 0f;
                }
            }
        }

        public void ApplyDamage(int dmg)
        {
            if (dmg <= 0) return;
            if (_dead) return;
            _currentHealth -= dmg;
            if (_currentHealth <= 0)
            {
                _dead = true;
                OnDeath?.Invoke();
                if (OnDeath == null)
                    Destroy(gameObject);
            }
        }

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _view != null ? _view.Stats.MaxHealth : 0;

        public void SetHealth(int hp)
        {
            _currentHealth = Mathf.Clamp(hp, 1, Mathf.Max(1, MaxHealth));
        }

        // Called by player input when issuing manual movement/path commands
        public void NotifyManualMove()
        {
            _combatSteering = false;
            _usingFlowField = false;
        }

        public void ForceRepath()
        {
            _repathTimer = 0f;
            _pathPending = false;
            _pathPendingTimer = 0f;
        }

        public void AssignSquadTarget(UnitCombat target, float ttl = 1.5f)
        {
            if (target == null || target == this) return;
            if (target.Faction == this.Faction) return;
            _forcedTarget = target;
            _forcedTargetTimer = Mathf.Max(0.1f, ttl);
            _cachedTarget = target;
        }

        public void ClearForcedTarget()
        {
            _forcedTarget = null;
            _forcedTargetTimer = 0f;
        }

        public void ApplyBehaviorProfile()
        {
            if (!UseBehaviorProfile || BehaviorProfile == null) return;
            ApplyBehaviorProfile(BehaviorProfile);
        }

        public void ApplyBehaviorProfile(UnitBehaviorProfile profile)
        {
            if (profile == null) return;
            HoldPosition = profile.HoldPosition;
            UseAggroRange = profile.UseAggroRange;
            AggroRange = profile.AggroRange;
            UseLeash = profile.UseLeash;
            LeashRange = profile.LeashRange;
            PreferForcedTarget = profile.PreferForcedTarget;
        }

        public void ApplyCombatProfile()
        {
            if (!UseCombatProfile || CombatProfile == null) return;
            ApplyCombatProfile(CombatProfile);
        }

        public void ApplyCombatProfile(UnitCombatProfile profile)
        {
            if (profile == null) return;
            AttackRange = profile.AttackRange;
            AttackDamage = profile.AttackDamage;
            AttackCooldown = profile.AttackCooldown;
            RepathInterval = profile.RepathInterval;
            RepathIntervalFar = profile.RepathIntervalFar;
            RepathIntervalVeryFar = profile.RepathIntervalVeryFar;
            RepathFailCooldown = profile.RepathFailCooldown;
            ClusterSizeForRepath = profile.ClusterSizeForRepath;
            FarClusterDistance = profile.FarClusterDistance;
            ClusterSizeForRepath2 = profile.ClusterSizeForRepath2;
            FarClusterDistance2 = profile.FarClusterDistance2;
            UseClusterStepping = profile.UseClusterStepping;
            RepathJitter = profile.RepathJitter;
            InstantRepathOnTargetCellChange = profile.InstantRepathOnTargetCellChange;
            StallRepathSeconds = profile.StallRepathSeconds;
            UseFlowFields = profile.UseFlowFields;
            FlowFieldMinDistance = profile.FlowFieldMinDistance;
            FlowFieldStepInterval = profile.FlowFieldStepInterval;
            FlowFieldStepJitter = profile.FlowFieldStepJitter;
            TargetRefreshInterval = profile.TargetRefreshInterval;
            EngageStopMultiplier = profile.EngageStopMultiplier;
            JobTargetTtl = profile.JobTargetTtl;
            LostTargetGraceSeconds = profile.LostTargetGraceSeconds;
            LocalThreatOverrideMultiplier = profile.LocalThreatOverrideMultiplier;
            CombatTickInterval = profile.CombatTickInterval;
            CombatTickJitter = profile.CombatTickJitter;
            DisableOrcaWhenInRange = profile.DisableOrcaWhenInRange;
            OrcaDisableRangeMultiplier = profile.OrcaDisableRangeMultiplier;
            UseFormationOffsets = profile.UseFormationOffsets;
            FormationOffsetStartDistance = profile.FormationOffsetStartDistance;
            FormationSpacingHex = profile.FormationSpacingHex;
            FormationMaxRadiusHex = profile.FormationMaxRadiusHex;
            LogCombatResets = profile.LogCombatResets;
            MaxCombatResetLogsPerFrame = profile.MaxCombatResetLogsPerFrame;
        }

        public void SetSquad(int squadId, SquadMode mode)
        {
            _squadId = squadId;
            _squadMode = mode;
        }

        public void SetSquadMode(SquadMode mode)
        {
            _squadMode = mode;
        }

        public void SetFormationIndex(int index)
        {
            _formationIndex = index;
        }

        public void ClearSquad()
        {
            _squadId = 0;
            _squadMode = SquadMode.None;
            _formationIndex = -1;
        }

        private UnitCombat FindNearestEnemy()
        {
            UnitCombat best = null;
            float bestDist2 = float.MaxValue;
            Vector3 p = _tr != null ? _tr.position : transform.position;
            foreach (var uc in All)
            {
                if (uc == null || uc == this) continue;
                if (!uc.isActiveAndEnabled) continue;
                if (uc.Faction == this.Faction) continue;
                float d2 = (uc.transform.position - p).sqrMagnitude;
                if (d2 < bestDist2)
                {
                    best = uc;
                    bestDist2 = d2;
                }
            }
            return best;
        }

        internal void SetJobNearest(UnitCombat uc)
        {
            if (uc != null && (uc == this || uc.Faction == Faction || !uc.isActiveAndEnabled))
                uc = null;
            _jobNearest = uc;
            _jobNearestTimer = uc != null ? JobTargetTtl + Random.Range(0f, 0.1f) : 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, AttackRange);
        }

        private void EnsureCaches()
        {
            if (_tr == null) _tr = transform;
            if (_view == null) _view = GetComponent<UnitView>() ?? gameObject.AddComponent<UnitView>();
            if (_pm == null) _pm = Game.Presentation.Pathfinding.PathManager.Ensure();
            if (_hex == null) _hex = SharedHex();
        }

        private bool IsLocalThreat(UnitCombat target)
        {
            if (target == null) return false;
            if (_tr == null) _tr = transform;
            float maxDist = Mathf.Max(AttackRange * LocalThreatOverrideMultiplier, AttackRange);
            float d2 = (target.transform.position - _tr.position).sqrMagnitude;
            return d2 <= maxDist * maxDist;
        }

        private UnitCombat ResolveTarget()
        {
            if (_cachedTarget != null && (!_cachedTarget.isActiveAndEnabled || _cachedTarget.Faction == Faction))
            {
                _cachedTarget = null;
            }

            _targetRefreshTimer -= Time.deltaTime;
            if (_targetRefreshTimer > 0f && _cachedTarget != null)
                return _cachedTarget;

            UnitCombat jobTarget = null;
            TryGetJobTarget(out jobTarget);

            UnitCombat hashTarget = null;
            if (jobTarget == null)
                TryGetHashTarget(out hashTarget);

            UnitCombat localTarget = jobTarget ?? hashTarget;

            if (TryGetForcedTarget(out var forced))
            {
                if (PreferForcedTarget || localTarget == null)
                    _cachedTarget = forced;
                else
                    _cachedTarget = IsLocalThreat(localTarget) ? localTarget : forced;
                _targetRefreshTimer = TargetRefreshInterval;
                return _cachedTarget;
            }

            if (localTarget != null)
            {
                _cachedTarget = localTarget;
                _targetRefreshTimer = TargetRefreshInterval;
                return _cachedTarget;
            }

            _cachedTarget = null;
            _targetRefreshTimer = TargetRefreshInterval;
            return _cachedTarget;
        }

        private bool ShouldIgnoreTarget(float targetDist, bool isForced)
        {
            if (HoldPosition && targetDist > AttackRange)
                return true;
            if (!isForced && UseAggroRange && AggroRange > 0f && targetDist > AggroRange)
                return true;
            if (!isForced && UseLeash && LeashRange > 0f)
            {
                float homeDist = (_tr.position - _homePosition).magnitude;
                if (homeDist > LeashRange)
                    return true;
            }
            return false;
        }

        internal bool TryGetJobTarget(out UnitCombat target)
        {
            target = null;
            if (_jobNearestTimer <= 0f) return false;
            if (_jobNearest == null || !_jobNearest.isActiveAndEnabled) return false;
            if (_jobNearest.Faction == Faction) return false;
            var scheduler = UnitCombatJobScheduler.Instance;
            if (scheduler != null && scheduler.Disabled) return false;
            target = _jobNearest;
            return true;
        }

        private bool TryGetHashTarget(out UnitCombat target)
        {
            target = null;
            if (_occ == null) return false;
            if (_tr == null) _tr = transform;
            if (_occ.TryGetNearestEnemy(_tr.position, Faction, out var enemy))
            {
                target = enemy;
                return true;
            }
            return false;
        }

        private bool TryGetForcedTarget(out UnitCombat target)
        {
            target = null;
            if (_forcedTarget == null || _forcedTargetTimer <= 0f) return false;
            if (!_forcedTarget.isActiveAndEnabled) return false;
            if (_forcedTarget.Faction == Faction) return false;
            target = _forcedTarget;
            return true;
        }

        private void LogReset(string reason, UnitCombat target, float dist)
        {
            if (!LogCombatResets) return;
            int frame = Time.frameCount;
            if (frame != _resetLogFrame)
            {
                _resetLogFrame = frame;
                _resetLogsThisFrame = 0;
            }
            if (MaxCombatResetLogsPerFrame > 0 && _resetLogsThisFrame >= MaxCombatResetLogsPerFrame) return;
            _resetLogsThisFrame++;
            string targetName = target != null ? target.name : "none";
            Debug.LogWarning($"[CombatReset] unit={name} reason={reason} dist={dist:F2} target={targetName} frame={frame} pos={_tr?.position ?? transform.position}");
        }

        private static bool TryConsumeRepathBudget()
        {
            TouchBudgetFrame();
            if (RepathBudgetPerFrame <= 0) return true;
            if (_repathsThisFrame >= RepathBudgetPerFrame) return false;
            _repathsThisFrame++;
            return true;
        }

        private void InvalidatePendingPath()
        {
            _pathRequestId++;
            _pathPending = false;
            _pathPendingTimer = 0f;
        }

        private bool TryFlowFieldMove(Vector3 desired, float targetDist, bool pathActive, bool ignoreMinDistance)
        {
            if (!UseFlowFields) return false;
            if (!ignoreMinDistance && targetDist < FlowFieldMinDistance) return false;
            var mgr = Game.Presentation.Pathfinding.FlowFieldManager.Instance;
            if (mgr == null || !mgr.Enabled) return false;

            if (_usingFlowField)
            {
                if (_flowFieldTimer > 0f)
                {
                    _flowFieldTimer -= Time.deltaTime;
                    if (_flowFieldTimer > 0f && _view != null && _view.TryGetDestination(out _))
                        return true;
                }
            }
            else
            {
                if (_flowFieldTimer > 0f)
                    _flowFieldTimer -= Time.deltaTime;
            }

            if (!mgr.TryGetNextPoint(_tr.position, desired, Faction, out var next))
                return false;

            InvalidatePendingPath();
            if (pathActive && _follower != null && _follower.Source == UnitPathFollower.PathSource.Combat)
                _follower.Cancel();
            _pathPending = false;
            _view.SetDestination(next);
            _combatSteering = true;
            _usingFlowField = true;
            _flowFieldTimer = FlowFieldStepInterval + Random.Range(0f, FlowFieldStepJitter);
            return true;
        }

        private static void TouchBudgetFrame()
        {
            if (RepathBudgetPerFrame <= 0)
                return; // budgets disabled; skip frame tracking
            int frame = Time.frameCount;
            if (frame == _budgetFrame) return;
            _budgetFrame = frame;
            _repathsThisFrame = 0;
        }

        private static Game.Presentation.Pathfinding.HexPathfindingBootstrap SharedHex()
        {
            if (_sharedHex == null || !_sharedHex.isActiveAndEnabled)
                _sharedHex = UnityEngine.Object.FindAnyObjectByType<Game.Presentation.Pathfinding.HexPathfindingBootstrap>();
            return _sharedHex;
        }

        private struct Axial
        {
            public int q;
            public int r;
            public Axial(int q, int r)
            {
                this.q = q;
                this.r = r;
            }
        }

        private static Axial OddRToAxial(Vector2Int cell)
        {
            int q = cell.x - (cell.y - (cell.y & 1)) / 2;
            int r = cell.y;
            return new Axial(q, r);
        }

        private static Vector2Int AxialToOddR(Axial axial)
        {
            int col = axial.q + (axial.r - (axial.r & 1)) / 2;
            int row = axial.r;
            return new Vector2Int(col, row);
        }

        private static Axial FormationAxialOffset(int index, int spacing, int maxRadius)
        {
            if (index <= 0) return new Axial(0, 0);
            int n = index - 1;
            int ring = 1;
            while (n >= 6 * ring)
            {
                n -= 6 * ring;
                ring++;
            }
            if (maxRadius > 0)
                ring = Mathf.Min(ring, maxRadius);
            int side = ring > 0 ? n / ring : 0;
            int offset = ring > 0 ? n % ring : 0;
            var dirs = new Axial[]
            {
                new Axial(1, 0),
                new Axial(1, -1),
                new Axial(0, -1),
                new Axial(-1, 0),
                new Axial(-1, 1),
                new Axial(0, 1)
            };
            Axial pos = new Axial(ring, 0);
            for (int i = 0; i < side; i++)
            {
                pos.q += dirs[i].q * ring;
                pos.r += dirs[i].r * ring;
            }
            pos.q += dirs[side].q * offset;
            pos.r += dirs[side].r * offset;
            pos.q *= spacing;
            pos.r *= spacing;
            return pos;
        }

        private static bool HasEnemyForFaction(Game.Domain.Units.Faction faction)
        {
            switch (faction)
            {
                case Game.Domain.Units.Faction.Player:
                    return _enemyCount > 0;
                case Game.Domain.Units.Faction.Enemy:
                    return _playerCount > 0;
                default:
                    return _playerCount > 0 || _enemyCount > 0;
            }
        }

        private static void RegisterFaction(Game.Domain.Units.Faction faction)
        {
            if (faction == Game.Domain.Units.Faction.Player) _playerCount++;
            else if (faction == Game.Domain.Units.Faction.Enemy) _enemyCount++;
        }

        private static void UnregisterFaction(Game.Domain.Units.Faction faction)
        {
            if (faction == Game.Domain.Units.Faction.Player) _playerCount = Mathf.Max(0, _playerCount - 1);
            else if (faction == Game.Domain.Units.Faction.Enemy) _enemyCount = Mathf.Max(0, _enemyCount - 1);
        }
    }
}


