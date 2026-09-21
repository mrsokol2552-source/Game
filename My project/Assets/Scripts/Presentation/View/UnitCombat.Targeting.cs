/*
@file: My project/Assets/Scripts/Presentation/View/UnitCombat.Targeting.cs
@module: presentation.combat.unit.targeting
@purpose: Holds target acquisition, target arbitration, faction overrides, and combat-facing helper methods for UnitCombat.
@entry: UCOM-05, UCOM-06, UCOM-07, UnitCombat.ResolveTarget
@api: partial class implementation for UnitCombat
@deps: UnitCombatJobScheduler, OccupancyHash, UnitView, UnitSpriteAnimator, PathManager, HexPathfindingBootstrap
@data: cached targets, repath budgets, faction counts, crouch/facing state
@perf: hotpath; runs from UnitCombat.Update and must stay allocation-free
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual battle verification
@config: inspector combat targeting/avoidance settings, UnitCombatProfile, UnitBehaviorProfile
@assets: none directly; updates facing/crouch state consumed by view/animation components
@notes: keep forced-target arbitration and local-threat override rules here so Update remains readable during combat refactors
*/

using Game.Domain.Units;
using Game.Presentation.Performance;
using Game.Presentation.Pathfinding;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-TARGETING]
// Logical block: Scripts/Presentation/View/UnitCombat.Targeting.

namespace Game.Presentation.View
{
    public partial class UnitCombat
    {
        // [UCOM-05]
        // Local target discovery and cache refresh helpers.
        private UnitCombat FindNearestEnemy()
        {
            UnitCombat best = null;
            float bestDist2 = float.MaxValue;
            Vector3 p = _tr != null ? _tr.position : transform.position;
            foreach (var uc in All)
            {
                if (uc == null || uc == this) continue;
                if (!uc.isActiveAndEnabled) continue;
                if (uc.Faction == Faction) continue;
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
            if (_pm == null) _pm = PathManager.Ensure();
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

        // [UCOM-06]
        // Target arbitration between forced targets, job/hash candidates, and local threat overrides.
        private UnitCombat ResolveTarget()
        {
            if (_cachedTarget != null && (!_cachedTarget.isActiveAndEnabled || _cachedTarget.Faction == Faction))
                _cachedTarget = null;

            _targetRefreshTimer -= Time.deltaTime;
            if (_targetRefreshTimer > 0f && _cachedTarget != null)
                return _cachedTarget;

            if (PreferForcedTarget && TryGetForcedTarget(out var preferredForced))
            {
                _cachedTarget = preferredForced;
                _targetRefreshTimer = TargetRefreshInterval;
                return _cachedTarget;
            }

            UnitCombat jobTarget = null;
            TryGetJobTarget(out jobTarget);

            UnitCombat hashTarget = null;
            if (jobTarget == null)
                TryGetHashTarget(out hashTarget);

            UnitCombat fallbackTarget = null;
            if (jobTarget == null && hashTarget == null)
                fallbackTarget = FindNearestEnemy();

            UnitCombat localTarget = jobTarget ?? hashTarget ?? fallbackTarget;
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
            return null;
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

        // [UCOM-07]
        // Profile/faction override layer that normalizes ranged-vs-melee behavior per faction.
        private void ApplyFactionOverrides()
        {
            if (!UseFactionOverrides) return;
            if (Faction == Game.Domain.Units.Faction.Player)
            {
                AttackRange = PlayerAttackRange;
                DisableOrcaWhenInRange = PlayerDisableOrcaInRange;
            }
            else if (Faction == Game.Domain.Units.Faction.Enemy)
            {
                AttackRange = EnemyAttackRange;
                DisableOrcaWhenInRange = EnemyDisableOrcaInRange;
            }
        }

        private static bool TryConsumeRepathBudget()
        {
            TouchBudgetFrame();
            if (RepathBudgetPerFrame <= 0) return true;
            if (_repathsThisFrame >= RepathBudgetPerFrame) return false;
            _repathsThisFrame++;
            return true;
        }

        private bool TryConsumeCombatTickBudget()
        {
            if (CombatTickBudgetPerFrame <= 0) return true;
            int unitCount = Mathf.Max(1, All.Count);
            if (unitCount <= CombatTickBudgetPerFrame) return true;

            int slotCount = Mathf.Max(1, Mathf.CeilToInt(unitCount / (float)CombatTickBudgetPerFrame));
            return ((_combatBudgetSlot + Time.frameCount) % slotCount) == 0;
        }

        private void FaceTarget(Vector3 targetPos)
        {
            if (_view == null) return;
            Vector3 dir = targetPos - _tr.position;
            if (dir.sqrMagnitude <= 0.0001f) return;
            _view.ApplyFacing(dir, CombatTickInterval);
        }

        private bool HasFriendlyTooClose(float radius)
        {
            if (radius <= 0.01f) return false;
            float r2 = radius * radius;
            foreach (var uc in All)
            {
                if (uc == null || uc == this || uc.Faction != Faction) continue;
                float d2 = (uc.transform.position - _tr.position).sqrMagnitude;
                if (d2 <= r2) return true;
            }

            return false;
        }

        private void TryCrouchWhenBlocked(Vector3 targetPos)
        {
            if (!UseCrouchWhenBlocked || _anim == null) return;
            Vector3 dir = targetPos - _tr.position;
            dir.z = 0f;
            if (dir.sqrMagnitude <= 0.0001f) return;
            dir.Normalize();
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
            float maxDist2 = CrouchBehindDistance * CrouchBehindDistance;
            foreach (var uc in All)
            {
                if (uc == null || uc == this || uc.Faction != Faction) continue;
                Vector3 delta = uc.transform.position - _tr.position;
                delta.z = 0f;
                if (delta.sqrMagnitude > maxDist2) continue;
                float behind = Vector3.Dot(delta, -dir);
                if (behind <= 0f) continue;
                float lateral = Mathf.Abs(Vector3.Dot(delta, perp));
                if (lateral > CrouchLateralTolerance) continue;
                _anim.RequestCrouch(CrouchBlockedSeconds);
                break;
            }
        }

        private void InvalidatePendingPath()
        {
            _pathRequestId++;
            _pathPending = false;
            _pathPendingTimer = 0f;
        }

        private static bool HasEnemyForFaction(Faction faction)
        {
            switch (faction)
            {
                case Faction.Player:
                    return _enemyCount > 0;
                case Faction.Enemy:
                    return _playerCount > 0;
                default:
                    return _playerCount > 0 || _enemyCount > 0;
            }
        }

        private static void RegisterFaction(Faction faction)
        {
            if (faction == Faction.Player) _playerCount++;
            else if (faction == Faction.Enemy) _enemyCount++;
        }

        private static void UnregisterFaction(Faction faction)
        {
            if (faction == Faction.Player) _playerCount = Mathf.Max(0, _playerCount - 1);
            else if (faction == Faction.Enemy) _enemyCount = Mathf.Max(0, _enemyCount - 1);
        }
    }
}
