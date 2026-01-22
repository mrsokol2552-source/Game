using UnityEngine;

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

        private UnitView _unit;
        private UnitCombat _combat;
        private float _time;
        private bool _wasMoving;
        private int _lastDir;
        private AnimMode _mode = AnimMode.Idle;
        private bool _deathStarted;

        private enum AnimMode
        {
            Idle,
            Walk,
            Attack,
            Death
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

            Sprite frame = null;
            switch (_mode)
            {
                case AnimMode.Attack:
                    frame = AnimSet.GetFrame(AnimSet.AttackFrames, _lastDir, _time, AnimSet.AttackFps);
                    if (_time >= AnimSet.GetDuration(AnimSet.AttackFrames, AnimSet.AttackFps))
                    {
                        _time = 0f;
                        _mode = moving ? AnimMode.Walk : AnimMode.Idle;
                    }
                    break;
                case AnimMode.Death:
                    frame = AnimSet.GetFrame(AnimSet.DeathFrames, _lastDir, _time, AnimSet.DeathFps);
                    break;
                case AnimMode.Walk:
                    frame = AnimSet.GetFrame(AnimSet.WalkFrames, dirIndex, _time, AnimSet.WalkFps);
                    break;
                default:
                    frame = AnimSet.GetFrame(AnimSet.IdleFrames, dirIndex, _time, AnimSet.IdleFps);
                    break;
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
