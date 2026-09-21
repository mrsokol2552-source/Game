/*
@file: My project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs
@module: presentation.combat.unit
@purpose: Extracted hot-loop orchestration for combat ticks, target engagement, chase/repath logic, and stall recovery.
@entry: UCOM-03, UCOM-11, UCOM-12, UCOM-13
@api: UnitCombat partial update-loop implementation
@deps: UnitView, UnitPathFollower, PathManager, PathRequestQueue, EnemySquadManager, HexPathfindingBootstrap
@data: target memory, combat timers, repath state, ORCA toggles, stall recovery state
@perf: hotpath, large-N combat update, sensitive to target scans, path churn, and callback traffic
@thread: main thread only
@tests: My project/Assets/Tests/EditMode/UnitCombatTargetingTests.cs, My project/Assets/Tests/PlayMode/CombatPathResetTests.cs, My project/Assets/Tests/PlayMode/UnitCombatStallTests.cs
@config: CombatTickInterval, RepathInterval*, FlowField*, LostTargetGraceSeconds, FriendlySeparationRadius
@assets: unit prefabs with UnitCombat, UnitView, and UnitSpriteAnimator
@notes: extracted to isolate the combat hot loop before the later data-layer and shader migration work
*/

using Game.Presentation.Pathfinding;
using Game.Presentation.Performance;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-UPDATELOOP]
// Logical block: Scripts/Presentation/View/UnitCombat.UpdateLoop.

namespace Game.Presentation.View
{
    public partial class UnitCombat
    {
        // [UCOM-03]
        // Main combat tick orchestration: state refresh, target resolution, engage/no-target branches, and stall recovery.
        private void Update()
        {
            if (!TryAdvanceCombatTick())
                return;

            EnsureCaches();
            bool pathActive = _follower != null && _follower.HasPath;
            bool allowIndividualPaths = _squadMode == SquadMode.None || _squadMode == SquadMode.FreeCombat;
            bool allowPerUnitPaths = _squadMode == SquadMode.None;
            UpdatePendingPathState(allowPerUnitPaths);

            bool enemiesPresent = HasEnemyForFaction(Faction);
            if (TryHandleNoEnemies(enemiesPresent))
                return;

            UnitCombat target = ResolveValidatedTarget();
            if (target != null)
            {
                HandleTargetEngagement(target, ref pathActive, allowIndividualPaths, allowPerUnitPaths);
            }
            else
            {
                HandleNoTargetState(enemiesPresent, pathActive);
            }

            UpdateStallBreaker(target, pathActive);
        }

        // [UCOM-11]
        // Tick gate, timer maintenance, faction refresh, and pending-path state normalization before target work.
        private bool TryAdvanceCombatTick()
        {
            _combatTickTimer -= Time.deltaTime;
            if (_combatTickTimer > 0f)
                return false;

            if (DisableCombat)
                return false;

            if (!TryConsumeCombatTickBudget())
                return false;

            _combatTickTimer = CombatTickInterval + Random.Range(0f, CombatTickJitter);

            if (Faction != _lastFaction)
            {
                UnregisterFaction(_lastFaction);
                RegisterFaction(Faction);
                _lastFaction = Faction;
                ApplyFactionOverrides();
            }

            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - CombatTickInterval);

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

            if (_occ == null)
                _occ = OccupancyHash.Instance;

            return true;
        }

        private void UpdatePendingPathState(bool allowPerUnitPaths)
        {
            if (!allowPerUnitPaths && _pathPending)
                InvalidatePendingPath();

            if (_pathPendingTimer > 0f)
            {
                _pathPendingTimer -= Time.deltaTime;
                if (_pathPendingTimer <= 0f)
                    _pathPending = false;
            }
        }

        private bool TryHandleNoEnemies(bool enemiesPresent)
        {
            if (enemiesPresent || _forcedTarget != null)
                return false;

            if (_follower != null && _follower.Source == UnitPathFollower.PathSource.Combat)
                _follower.Cancel();

            if (_combatSteering)
                _view.ClearDestination("combat-no-enemies");

            _combatSteering = false;
            _hasLastCombatDestination = false;
            _pathPending = false;
            _lostTargetGraceTimer = 0f;
            InvalidatePendingPath();

            if (_view != null && DisableOrcaWhenInRange)
                _view.UseOrcaVelocity = true;

            return true;
        }

        private UnitCombat ResolveValidatedTarget()
        {
            UnitCombat target = ResolveTarget();
            if (target == null)
                return null;

            bool isForced = _forcedTarget != null && target == _forcedTarget;
            float distCheck = (_tr.position - target.transform.position).magnitude;
            if (!ShouldIgnoreTarget(distCheck, isForced))
                return target;

            _cachedTarget = null;
            return null;
        }

        // [UCOM-12]
        // Active target engagement: facing, ORCA toggles, chase/repath logic, formation offsets, and in-range attacks.
        private void HandleTargetEngagement(UnitCombat target, ref bool pathActive, bool allowIndividualPaths, bool allowPerUnitPaths)
        {
            _lostTargetGraceTimer = LostTargetGraceSeconds;
            _lastTargetPos = target.transform.position;
            _hasLastTargetPos = true;

            Vector3 targetPosition = target.transform.position;
            Vector3 myPosition = _tr.position;
            FaceTarget(targetPosition);

            if (_hex == null)
                _hex = SharedHex();

            Vector2Int targetCell = _hex != null ? _hex.WorldToGrid(targetPosition) : Vector2Int.zero;
            float dist = (targetPosition - myPosition).magnitude;
            float stopDist = Mathf.Max(AttackRange * 0.9f, 0.1f);

            if (_view != null && DisableOrcaWhenInRange)
            {
                float orcaDisableDist = AttackRange * Mathf.Max(1f, OrcaDisableRangeMultiplier);
                bool keepOrca = OrcaAvoidanceSystem.IsActive && KeepOrcaNearFriendlies && HasFriendlyTooClose(FriendlySeparationRadius);
                _view.UseOrcaVelocity = dist > orcaDisableDist || keepOrca;
            }
            else if (_view != null)
            {
                _view.UseOrcaVelocity = true;
            }

            float cancelDist = Mathf.Max(stopDist, AttackRange * 0.95f);
            if (dist <= cancelDist && pathActive)
            {
                if (_follower != null && _follower.Source == UnitPathFollower.PathSource.Combat)
                    _follower.Cancel();

                _view.ClearDestination("combat-in-range");
                LogReset("in-range-stop", target, dist);
                pathActive = false;
                _pathPending = false;
                InvalidatePendingPath();
            }

            if (dist > AttackRange)
            {
                HandleTargetOutOfRange(target, targetPosition, myPosition, targetCell, dist, stopDist, ref pathActive, allowIndividualPaths, allowPerUnitPaths);
                return;
            }

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
                TryCrouchWhenBlocked(targetPosition);
                target.ApplyDamage(AttackDamage);
                _cooldown = AttackCooldown;
            }
        }

        private void HandleTargetOutOfRange(UnitCombat target, Vector3 targetPosition, Vector3 myPosition, Vector2Int targetCell, float dist, float stopDist, ref bool pathActive, bool allowIndividualPaths, bool allowPerUnitPaths)
        {
            if (HoldPosition)
            {
                if (pathActive && _follower != null && _follower.Source == UnitPathFollower.PathSource.Combat)
                    _follower.Cancel();

                if (_combatSteering)
                    _view.ClearDestination("combat-hold-position");

                _combatSteering = false;
                _usingFlowField = false;
                return;
            }

            Vector3 dirToTarget = targetPosition - myPosition;
            if (dirToTarget.sqrMagnitude < 0.0001f)
                dirToTarget = Vector3.right;

            Vector3 desired = targetPosition - dirToTarget.normalized * stopDist;
            bool useSquadAnchor = false;
            EnemySquadManager squadMgr = EnemySquadManager.Instance;
            if (IsInSquad && _squadMode != SquadMode.FreeCombat && squadMgr != null)
            {
                if (squadMgr.TryGetSquadAnchor(_squadId, out Vector3 anchor, out _, out SquadMode squadMode) &&
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
                if (_hex == null)
                    _hex = SharedHex();

                if (_hex != null)
                {
                    Vector2Int anchorCell = _hex.WorldToGrid(desired);
                    Axial anchorAxial = OddRToAxial(anchorCell);
                    Axial offsetAxial = FormationAxialOffset(_formationIndex, Mathf.Max(1, FormationSpacingHex), FormationMaxRadiusHex);
                    Axial targetAxial = new Axial(anchorAxial.q + offsetAxial.q, anchorAxial.r + offsetAxial.r);
                    Vector2Int offsetCell = AxialToOddR(targetAxial);
                    offsetCell.x = Mathf.Clamp(offsetCell.x, 0, _hex.Width - 1);
                    offsetCell.y = Mathf.Clamp(offsetCell.y, 0, _hex.Height - 1);
                    desired = _hex.GridToWorld(offsetCell.x, offsetCell.y);
                    formationApplied = true;
                }
            }

            if (!formationApplied)
            {
                float jitterScale = dist <= AttackRange * 2f ? 0.2f : 0.35f;
                float jitter = AttackRange * jitterScale;
                int hash = GetInstanceID();
                float r = (Mathf.Sin(hash * 12.9898f) * 43758.5453f) % 1f;
                Vector3 perp = new Vector3(-dirToTarget.normalized.y, dirToTarget.normalized.x, 0f);
                desired += perp * ((r - 0.5f) * jitter);
            }

            if (_hex == null)
                _hex = SharedHex();

            if (_hex != null)
            {
                Vector2Int desiredCell = _hex.WorldToGrid(desired);
                Vector2Int currentCell = _hex.WorldToGrid(myPosition);
                if (desiredCell != currentCell)
                    desired = _hex.GridToWorld(desiredCell.x, desiredCell.y);
            }

            if (_pm != null && _pm.IsWorldOccupied(desired, _view, enemiesOnly: true) && _pm.TryFindNearestFreeWorld(desired, _view, 2, out Vector3 free))
                desired = free;

            bool forceFlowField = !allowIndividualPaths;
            if (TryFlowFieldMove(desired, dist, pathActive, forceFlowField))
                return;

            if (!allowIndividualPaths)
            {
                InvalidatePendingPath();
                if (pathActive && _follower != null && _follower.Source == UnitPathFollower.PathSource.Combat)
                    _follower.Cancel();

                TrySetCombatDestination(desired);
                return;
            }

            if (_repathTimer > 0f)
                _repathTimer -= Time.deltaTime;

            float delta2 = (desired - _lastDesired).sqrMagnitude;
            bool targetMovedCell = _hex != null && targetCell != _lastTargetCell;
            if (InstantRepathOnTargetCellChange && targetMovedCell && !_pathPending)
                _repathTimer = 0f;

            if (!pathActive && !_pathPending && dist > AttackRange * 0.9f)
            {
                TrySetCombatDestination(desired);
            }

            bool shouldBuildPath = allowPerUnitPaths
                && _repathTimer <= 0f
                && (!_pathPending || _pathPendingTimer <= 0f)
                && (!pathActive || targetMovedCell || delta2 > 0.05f * 0.05f);
            if (!shouldBuildPath)
                return;

            _usingFlowField = false;
            if (!TryConsumeRepathBudget())
            {
                _repathTimer = 0.05f + Random.Range(0f, RepathJitter);
                return;
            }

            if (_pm != null)
            {
                int cd2 = _pm.ClusterDistance(myPosition, targetPosition, ClusterSizeForRepath2);
                if (cd2 >= FarClusterDistance2 + 2 && !pathActive)
                {
                    _repathTimer = RepathIntervalVeryFar + Random.Range(0f, RepathJitter);
                    _combatSteering = true;
                    _lastTargetCell = targetCell;
                    return;
                }
            }

            if (_pm != null && _pm.IsWorldOccupied(desired, _view) && _pm.TryFindNearestFreeWorld(desired, _view, 2, out Vector3 alt))
                desired = alt;

            Vector3 buildTarget = desired;
            if (UseClusterStepping && _pm != null && _pm.ClusterDistance(myPosition, targetPosition, ClusterSizeForRepath) > 0)
            {
                if (_pm.TryGetClusterEdgeTarget(myPosition, targetPosition, ClusterSizeForRepath, _view, out Vector3 edge))
                    buildTarget = edge;
            }

            Game.Presentation.Pathfinding.PathRequestQueue.Ensure();
            bool pathActiveLocal = pathActive;
            Vector3 desiredLocal = desired;
            UnitCombat targetRef = target;
            _pathPending = true;
            _pathPendingTimer = 0.35f;
            int reqId = ++_pathRequestId;
            Game.Presentation.Pathfinding.PathRequestQueue.Instance.Enqueue(_view, buildTarget, allowDiag: true, smooth: true, onDone: (built, worldPath) =>
            {
                if (this == null || !isActiveAndEnabled || _view == null)
                {
                    if (worldPath != null)
                        Game.Presentation.Pathfinding.PathManager.ReturnWorldList(worldPath);
                    return;
                }

                if (reqId != _pathRequestId)
                {
                    if (worldPath != null)
                        Game.Presentation.Pathfinding.PathManager.ReturnWorldList(worldPath);
                    return;
                }

                _pathPending = false;
                if (_cachedTarget != targetRef || targetRef == null)
                {
                    if (worldPath != null)
                        Game.Presentation.Pathfinding.PathManager.ReturnWorldList(worldPath);
                    return;
                }

                if (!built && buildTarget != desiredLocal && _pm != null)
                    built = _pm.BuildPath(_view, desiredLocal, allowDiag: true, smooth: true, autoFit: false, out worldPath, blockFriendlies: false);

                if (built)
                {
                    _usingFlowField = false;
                    if (_follower == null)
                        _follower = gameObject.AddComponent<UnitPathFollower>();

                    _follower.SetWorldPath(worldPath, UnitPathFollower.PathSource.Combat);
                    Game.Presentation.Pathfinding.PathManager.ReturnWorldList(worldPath);
                    _lastDesired = desiredLocal;

                    float interval = RepathInterval;
                    if (_pm != null)
                    {
                        int cd1 = _pm.ClusterDistance(myPosition, targetPosition, ClusterSizeForRepath);
                        int cd2 = _pm.ClusterDistance(myPosition, targetPosition, ClusterSizeForRepath2);
                        if (cd2 >= FarClusterDistance2)
                            interval = RepathIntervalVeryFar;
                        else if (cd1 >= FarClusterDistance)
                            interval = RepathIntervalFar;

                        float distHex = _hex != null ? (myPosition - targetPosition).magnitude / (_hex.HexSize * 0.75f) : 0f;
                        if (distHex > 100f)
                            interval *= 3f;
                        else if (distHex > 50f)
                            interval *= 2f;
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
                        TrySetCombatDestination(desiredLocal);
                    }
                }
            });
        }

        private bool TrySetCombatDestination(Vector3 desired)
        {
            if (_view == null)
                return false;

            float refreshDistance = Mathf.Clamp(AttackRange * 0.15f, 0.15f, 0.5f);
            float refreshDistance2 = refreshDistance * refreshDistance;
            if (_view.TryGetDestination(out Vector3 current) && (desired - current).sqrMagnitude < refreshDistance2)
                return false;

            if (_hasLastCombatDestination && (desired - _lastCombatDestination).sqrMagnitude < refreshDistance2)
            {
                float arrivedDistance = Mathf.Max(refreshDistance, _view.GetMovementSettings().StopDistance * 2f);
                if ((_tr.position - _lastCombatDestination).sqrMagnitude <= arrivedDistance * arrivedDistance)
                    return false;
            }

            _view.SetDestination(desired);
            _lastCombatDestination = desired;
            _hasLastCombatDestination = true;
            _combatSteering = true;
            return true;
        }

        // [UCOM-13]
        // No-target grace handling and stall-recovery nudges when combat steering loses or cannot reach a target.
        private void HandleNoTargetState(bool enemiesPresent, bool pathActive)
        {
            if (_view != null && DisableOrcaWhenInRange)
                _view.UseOrcaVelocity = true;

            if (enemiesPresent && _lostTargetGraceTimer > 0f)
            {
                _lostTargetGraceTimer = Mathf.Max(0f, _lostTargetGraceTimer - Time.deltaTime);
                if (_hasLastTargetPos && _view != null && _view.TryGetDestination(out Vector3 dest))
                {
                    Vector3 pos = _tr.position;
                    Vector3 toDest = dest - pos;
                    toDest.z = 0f;
                    Vector3 toLast = _lastTargetPos - pos;
                    toLast.z = 0f;
                    if (toDest.sqrMagnitude > 0.0001f && toLast.sqrMagnitude > 0.0001f && Vector3.Dot(toDest.normalized, toLast.normalized) < 0f)
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

                _stallTimer = 0f;
                return;
            }

            _lostTargetGraceTimer = 0f;
            if (pathActive && _follower.Source == UnitPathFollower.PathSource.Combat)
                _follower.Cancel();

            if (_combatSteering)
            {
                _view.ClearDestination("combat-no-enemies");
                LogReset("no-enemies", null, 0f);
                if (_hex == null)
                    _hex = SharedHex();
                if (_hex != null)
                {
                    Vector2Int cell = _hex.WorldToGrid(_tr.position);
                    _tr.position = _hex.GridToWorld(cell.x, cell.y);
                }
            }

            _combatSteering = false;
            _usingFlowField = false;
            _stallTimer = 0f;
        }

        private void UpdateStallBreaker(UnitCombat target, bool pathActive)
        {
            if (target == null)
                return;

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

                return;
            }

            _stallTimer = 0f;
        }
    }
}
