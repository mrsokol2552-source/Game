using UnityEngine;

/*
@file: My project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.Combat.cs
@module: presentation.view.units.animation.combat
@purpose: Hosts attack/death animation triggers and crouch requests driven by UnitCombat.
@entry: USPA-03, UnitSpriteAnimator.HandleAttack, UnitSpriteAnimator.HandleDeath
@api: internal partial of UnitSpriteAnimator
@deps: UnitCombat, DirectionalAnimationSet, UnitView
@data: attack/death mode transitions, crouch timer, destroy delay
@perf: event-driven, not hot-path
@thread: main thread only
@tests: scripts/run_all_repo_audits.py, Unity script recompile, manual attack/death verification
@config: DeathDestroyExtraDelay, UseCrouchAfterAttack, CrouchAfterAttackSeconds
*/

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR-COMBAT]
// Logical block: Scripts/Presentation/View/UnitSpriteAnimator.Combat.

namespace Game.Presentation.View
{
    public partial class UnitSpriteAnimator
    {
        // [USPA-03]
        // Combat-driven animation events and external crouch requests.
        private void HandleAttack()
        {
            if (_deathStarted) return;
            if (AnimSet == null || AnimSet.AttackFrames == null || AnimSet.AttackFrames.Length == 0)
                return;
            _lastDir = AnimSet.GetDirectionIndex(_unit != null ? _unit.GetLastDirection() : Vector2.right);
            _mode = AnimMode.Attack;
            _time = 0f;
            if (UseCrouchAfterAttack && CrouchAfterAttackSeconds > 0f)
                _crouchTimer = Mathf.Max(_crouchTimer, CrouchAfterAttackSeconds);
        }

        public void RequestCrouch(float seconds)
        {
            if (seconds <= 0f) return;
            _crouchTimer = Mathf.Max(_crouchTimer, seconds);
        }

        private void HandleDeath()
        {
            if (_deathStarted) return;
            _deathStarted = true;
            _lastDir = AnimSet.GetDirectionIndex(_unit != null ? _unit.GetLastDirection() : Vector2.right);
            _mode = AnimMode.Death;
            _time = 0f;
            if (_combat != null)
                _combat.enabled = false;

            if (AnimSet != null && AnimSet.DeathFrames != null && AnimSet.DeathFrames.Length > 0)
            {
                float delay = AnimSet.GetDuration(AnimSet.DeathFrames, AnimSet.DeathFps) + Mathf.Max(0f, DeathDestroyExtraDelay);
                if (delay > 0f)
                    Destroy(gameObject, delay);
                else
                    Destroy(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
