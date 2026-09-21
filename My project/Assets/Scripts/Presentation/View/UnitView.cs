/*
@file: My project/Assets/Scripts/Presentation/View/UnitView.cs
@module: presentation.view.unit
@purpose: Holds per-unit movement state, destination/override API, and shared runtime flags consumed by movement, combat, and avoidance systems.
@entry: UnitView.SetDestination, UVEW-01, UVEW-02
@api: MonoBehaviour used by movement, pathfinding, combat, ORCA, and culling systems
@deps: MovementSettings, PathProfiler, MovementJobSystem, CompositionRoot
@data: destination, steering/velocity overrides, speed, facing state, sorting configuration
@perf: hotpath; destination and override access are touched by several runtime systems each frame
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual combat/movement verification
@config: Movement, UseMovementJobs, UseSteering, ORCA and sorting inspector settings
@assets: SpriteRenderer on the unit GameObject
@notes: gameplay steering and visual sorting are intentionally being separated to make future render/data migration safer
*/

using System.Collections.Generic;
using Game.Domain.Units;
using UnityEngine;
using Game.Presentation.Performance;
using Game.Presentation.Bootstrap;

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITVIEW]
// Logical block: Scripts/Presentation/View/UnitView.

namespace Game.Presentation.View
{
    public partial class UnitView : MonoBehaviour
    {
        // [UVEW-01]
        // Per-unit state, movement/avoidance/sorting config, and lifecycle membership in UnitView.All.
        public static readonly HashSet<UnitView> All = new HashSet<UnitView>();

        public UnitStats Stats = new UnitStats();
        [Header("Movement")]
        public MovementSettings Movement; // optional; if null uses defaults
        [Header("Steering")]
        [Tooltip("Apply steering offset from avoidance systems.")]
        public bool UseSteering = true;
        [Tooltip("Scale of steering vector before blending with desired direction.")]
        public float SteeringInfluence = 1f;
        [Tooltip("Use job-based movement updates when available.")]
        public bool UseMovementJobs = true;
        [Header("ORCA/RVO")]
        [Tooltip("Accept velocity overrides from ORCA/RVO avoidance.")]
        public bool UseOrcaVelocity = true;
        [Tooltip("Allow using ORCA velocity overrides from recent frames (0 = current frame only).")]
        public int VelocityOverrideMaxAgeFrames = 1;
        [Range(0f, 1f)]
        [Tooltip("Priority (0=normal, 1=highest) reduces avoidance responsibility.")]
        public float OrcaPriority = 0f;
        [Header("Facing")]
        [Tooltip("Flip sprite on X when moving left/right instead of rotating the transform.")]
        public bool MirrorSpriteX = true;
        [Tooltip("Minimum |dir.x| before mirroring is applied.")]
        public float MirrorDeadZone = 0.05f;
        [Header("Diagnostics")]
        [Tooltip("Log rapid re-commands to nearby points (helps detect jitter).")]
        public bool LogJitteryCommands = false;
        [Tooltip("Distance threshold to consider commands as jitter (world units).")]
        public float JitterDistance = 0.05f;
        [Tooltip("Time window in seconds to group jittery commands.")]
        public float JitterWindow = 0.2f;
        [Header("Rendering")]
        [Tooltip("Enable Y-based sorting to reduce flicker when units overlap.")]
        public bool UseYSorting = true;
        [Tooltip("Sorting order units per 1 world unit of Y.")]
        public float SortOrderPerWorldUnit = 10f;
        [Tooltip("Base sorting order applied before Y offset.")]
        public int SortingOrderBase = 0;
        [Tooltip("Extra offset applied to sorting order to keep units above tilemaps on large maps.")]
        public int SortingOrderOffset = 10000;
        public bool UseCompositionRootSorting = true;
        public bool AddSortingTieBreaker = true;

        private Vector3? destination;
        private float _currentSpeed;
        private Vector3 _lastDir;
        private SpriteRenderer _sr;
        private Vector3 _lastDest;
        private float _lastDestTime;
        private Vector3 _steering;
        private int _steeringFrame = -1;
        private Vector3 _velocityOverride;
        private int _velocityOverrideFrame = -1;
        private bool _sortingInitialized;
        private int _sortingTie;
        public static bool EnableJitterLog = false;

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        // [UVEW-02]
        // Destination, steering, velocity-override, and state accessors used by runtime systems.
        public void SetDestination(Vector3 target)
        {
            if ((target - transform.position).sqrMagnitude < 0.0001f)
                return; // ignore tiny re-commands to avoid jitter
            if (destination.HasValue && (target - destination.Value).sqrMagnitude < 0.0001f)
                return;
            Game.Presentation.Pathfinding.PathProfiler.CountCommand();
            if (LogJitteryCommands && EnableJitterLog)
            {
                float now = Time.time;
                if (destination.HasValue)
                {
                    float dist = (target - destination.Value).magnitude;
                    if (dist <= JitterDistance && (now - _lastDestTime) <= JitterWindow)
                    {
                        Game.Presentation.Pathfinding.PathProfiler.CountJitter();
                        Debug.LogWarning($"[UnitView] Jittery command detected for {name}: dist={dist:F3}, dt={(now - _lastDestTime):F3}, from={destination.Value} to={target}");
                    }
                }
                _lastDest = target;
                _lastDestTime = now;
            }
            destination = target;
        }

        public void ClearDestination(string reason = "unspecified")
        {
            if (!destination.HasValue)
                return;
            destination = null;
            Game.Presentation.Pathfinding.PathProfiler.CountPathReset(reason);
        }

        public bool TryGetDestination(out Vector3 target)
        {
            if (destination.HasValue)
            {
                target = destination.Value;
                return true;
            }
            target = default;
            return false;
        }

        public bool HasDestination => destination.HasValue;

        public void SetSteering(Vector3 steer, int applyFrame)
        {
            _steering = steer;
            _steeringFrame = applyFrame;
        }

        public void SetSteering(Vector3 steer)
        {
            SetSteering(steer, Time.frameCount);
        }

        public bool TryGetSteering(out Vector3 steer)
        {
            if (UseSteering && _steeringFrame == Time.frameCount)
            {
                steer = _steering;
                return true;
            }
            steer = default;
            return false;
        }

        public void SetVelocityOverride(Vector3 velocity, int applyFrame)
        {
            _velocityOverride = velocity;
            _velocityOverrideFrame = applyFrame;
        }

        public void SetVelocityOverride(Vector3 velocity)
        {
            SetVelocityOverride(velocity, Time.frameCount);
        }

        public bool TryGetVelocityOverride(out Vector3 velocity)
        {
            int maxAge = Mathf.Max(0, VelocityOverrideMaxAgeFrames);
            if (UseOrcaVelocity && _velocityOverrideFrame >= 0 && (Time.frameCount - _velocityOverrideFrame) <= maxAge)
            {
                velocity = _velocityOverride;
                return true;
            }
            velocity = default;
            return false;
        }

        public float GetSpeed() => _currentSpeed;

        public void SetSpeed(float speed)
        {
            _currentSpeed = Mathf.Max(0f, speed);
        }

        public Vector3 GetLastDirection()
        {
            if (_lastDir.sqrMagnitude > 0.0001f)
                return _lastDir;
            return Vector3.right;
        }

        public void ClearDestinationSilent()
        {
            destination = null;
        }

        public MovementSettings GetMovementSettings() => MovementOrDefault;
    }
}
