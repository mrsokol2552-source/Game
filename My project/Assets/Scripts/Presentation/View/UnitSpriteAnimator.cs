using UnityEngine;

/*
@file: My project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.cs
@module: presentation.view.units.animation
@purpose: Hosts shared sprite-animation state, renderer binding, and combat event subscription lifecycle for units.
@entry: USPA-01, UnitSpriteAnimator.Awake, UnitSpriteAnimator.OnEnable, UnitSpriteAnimator.OnDisable
@api: UnitSpriteAnimator
@deps: DirectionalAnimationSet, UnitView, UnitCombat, SpriteRenderer
@data: animation mode, direction cache, crouch timer, death state, renderer reference
@perf: lightweight per-unit state holder; keep event wiring and cached references allocation-free
@thread: main thread only
@tests: scripts/run_all_repo_audits.py, Unity script recompile, manual animation verification
@config: AnimSet, MoveSpeedThreshold, DeathDestroyExtraDelay, UseScaledTime, UseCrouchAfterAttack, CrouchAfterAttackSeconds, ForceCrouch
*/

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR]
// Logical block: Scripts/Presentation/View/UnitSpriteAnimator.

namespace Game.Presentation.View
{
    [RequireComponent(typeof(SpriteRenderer))]
    public partial class UnitSpriteAnimator : MonoBehaviour
    {
        // [USPA-01]
        // Animator state, component wiring, and event subscription lifecycle.
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

    }
}
