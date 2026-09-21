/*
@file: My project/Assets/Scripts/Presentation/View/UnitCombat.State.cs
@module: presentation.combat.unit
@purpose: Damage, forced-target control, manual-move resets, and profile application extracted from UnitCombat.
@entry: UCOM-09, UCOM-10
@api: UnitCombat partial state/control helpers
@deps: UnitView, UnitCombatProfile, UnitBehaviorProfile
@data: health, forced-target cache, repath state, behavior/combat tuning values
@perf: lightweight state mutation; not the primary combat hot-loop
@thread: main thread only
@tests: manual combat, save/load, and profile-application verification
@config: UseCombatProfile, CombatProfile, UseBehaviorProfile, BehaviorProfile
@assets: profile ScriptableObjects referenced by unit prefabs
@notes: extracted to keep UnitCombat.cs focused on lifecycle and main update orchestration
*/

using Game.Infrastructure.Configs;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-STATE]
// Logical block: Scripts/Presentation/View/UnitCombat.State.

namespace Game.Presentation.View
{
    public partial class UnitCombat
    {
        // [UCOM-09]
        // Damage application, health accessors, manual-move resets, and forced-target control.
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

        public void NotifyManualMove()
        {
            _combatSteering = false;
            _usingFlowField = false;
            _hasLastCombatDestination = false;
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

        // [UCOM-10]
        // Behavior/combat profile application and override propagation.
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
            ApplyFactionOverrides();
        }
    }
}
