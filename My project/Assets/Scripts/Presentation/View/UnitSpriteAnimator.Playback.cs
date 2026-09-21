using UnityEngine;

/*
@file: My project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.Playback.cs
@module: presentation.view.units.animation.playback
@purpose: Hosts per-frame directional sprite playback, idle/walk/crouch state transitions, and frame selection.
@entry: USPA-02, UnitSpriteAnimator.Update
@api: internal partial of UnitSpriteAnimator
@deps: DirectionalAnimationSet, UnitView, SpriteRenderer
@data: animation mode, direction index, crouch timer, local time cursor
@perf: per-frame on every animated unit; keep branchy logic compact and allocation-free
@thread: main thread only
@tests: scripts/run_all_repo_audits.py, Unity script recompile, manual animation verification
@config: MoveSpeedThreshold, UseScaledTime, UseCrouchAfterAttack, ForceCrouch
*/

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR-PLAYBACK]
// Logical block: Scripts/Presentation/View/UnitSpriteAnimator.Playback.

namespace Game.Presentation.View
{
    public partial class UnitSpriteAnimator
    {
        // [USPA-02]
        // Per-frame playback and locomotion/crouch state machine.
        private void Update()
        {
            if (AnimSet == null || TargetRenderer == null)
                return;

            Vector2 dir = Vector2.right;
            float speed = 0f;
            if (_unit != null)
            {
                dir = _unit.GetLastDirection();
                speed = _unit.GetSpeed();
            }

            int dirIndex = AnimSet.GetDirectionIndex(dir);
            bool moving = speed > MoveSpeedThreshold;

            if ((_mode == AnimMode.Idle || _mode == AnimMode.Walk) && (moving != _wasMoving || dirIndex != _lastDir))
            {
                _time = 0f;
                _wasMoving = moving;
                _lastDir = dirIndex;
                _mode = moving ? AnimMode.Walk : AnimMode.Idle;
            }

            float dt = UseScaledTime ? Time.deltaTime : Time.unscaledDeltaTime;
            _time += dt;
            if (_crouchTimer > 0f)
                _crouchTimer = Mathf.Max(0f, _crouchTimer - dt);

            Sprite frame = null;
            if (_mode == AnimMode.Attack)
            {
                frame = AnimSet.GetFrame(AnimSet.AttackFrames, _lastDir, _time, AnimSet.AttackFps);
                if (_time >= AnimSet.GetDuration(AnimSet.AttackFrames, AnimSet.AttackFps))
                {
                    _time = 0f;
                    _mode = moving ? AnimMode.Walk : AnimMode.Idle;
                }
            }
            else if (_mode == AnimMode.Death)
            {
                frame = AnimSet.GetFrame(AnimSet.DeathFrames, _lastDir, _time, AnimSet.DeathFps);
            }
            else
            {
                bool hasCrouchIdle = AnimSet.CrouchIdleFrames != null && AnimSet.CrouchIdleFrames.Length > 0;
                bool hasCrouchRun = AnimSet.CrouchRunFrames != null && AnimSet.CrouchRunFrames.Length > 0;
                bool useCrouch = ForceCrouch || (UseCrouchAfterAttack && _crouchTimer > 0f);
                AnimMode desired;
                if (useCrouch && (hasCrouchIdle || hasCrouchRun))
                    desired = moving && hasCrouchRun ? AnimMode.CrouchRun : AnimMode.CrouchIdle;
                else
                    desired = moving ? AnimMode.Walk : AnimMode.Idle;

                if (desired != _mode || dirIndex != _lastDir || moving != _wasMoving)
                {
                    _mode = desired;
                    _time = 0f;
                    _lastDir = dirIndex;
                    _wasMoving = moving;
                }

                switch (_mode)
                {
                    case AnimMode.CrouchRun:
                        frame = AnimSet.GetFrame(AnimSet.CrouchRunFrames, dirIndex, _time, AnimSet.CrouchRunFps);
                        break;
                    case AnimMode.CrouchIdle:
                        frame = AnimSet.GetFrame(AnimSet.CrouchIdleFrames, dirIndex, _time, AnimSet.CrouchIdleFps);
                        break;
                    case AnimMode.Walk:
                        frame = AnimSet.GetFrame(AnimSet.WalkFrames, dirIndex, _time, AnimSet.WalkFps);
                        break;
                    default:
                        frame = AnimSet.GetFrame(AnimSet.IdleFrames, dirIndex, _time, AnimSet.IdleFps);
                        break;
                }
            }

            if (frame != null)
                TargetRenderer.sprite = frame;
        }
    }
}
