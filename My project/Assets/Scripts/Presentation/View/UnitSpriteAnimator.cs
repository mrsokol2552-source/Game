using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR]
// Logical block: Scripts/Presentation/View/UnitSpriteAnimator.

namespace Game.Presentation.View
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class UnitSpriteAnimator : MonoBehaviour
    {
        public DirectionalAnimationSet AnimSet;
        [Tooltip("Speed threshold to switch to Walk animation.")]
        public float MoveSpeedThreshold = 0.05f;
        [Tooltip("Delay before destroying unit after death animation (seconds).")]
        public float DeathDestroyExtraDelay = 0f;
        [Tooltip("If false, uses unscaled time (UI/time pause safe).")]
        public bool UseScaledTime = true;
        public SpriteRenderer TargetRenderer;
        [Header("Crouch")]
        public bool UseCrouchAfterAttack = true;
        [Tooltip("How long to stay crouched after an attack (seconds).")]
        public float CrouchAfterAttackSeconds = 0.4f;
        [Tooltip("Force crouch regardless of attacks (debug).")]
        public bool ForceCrouch = false;

        private UnitView _unit;
        private UnitCombat _combat;
        private float _time;
        private bool _wasMoving;
        private int _lastDir;
        private AnimMode _mode = AnimMode.Idle;
        private bool _deathStarted;
        private float _crouchTimer;

        private enum AnimMode
        {
            Idle,
            Walk,
            Attack,
            Death,
            CrouchIdle,
            CrouchRun
        }

        private void Awake()
        {
            _unit = GetComponent<UnitView>();
            _combat = GetComponent<UnitCombat>();
            if (TargetRenderer == null)
                TargetRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (_combat != null)
            {
                _combat.OnAttack += HandleAttack;
                _combat.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_combat != null)
            {
                _combat.OnAttack -= HandleAttack;
                _combat.OnDeath -= HandleDeath;
            }
        }

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