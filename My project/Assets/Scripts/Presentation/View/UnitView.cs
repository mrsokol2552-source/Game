using System.Collections.Generic;
using Game.Domain.Units;
using UnityEngine;
using Game.Presentation.Performance;

namespace Game.Presentation.View
{
    public class UnitView : MonoBehaviour
    {
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

        public void SetDestination(Vector3 target)
        {
            if ((target - transform.position).sqrMagnitude < 0.0001f)
                return; // ignore tiny re-commands to avoid jitter
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

        public void ApplyFacing(Vector3 dir, float deltaTime)
        {
            if (dir.sqrMagnitude <= 0.0001f) return;
            _lastDir = dir;
            if (MirrorSpriteX && _sr != null)
            {
                if (Mathf.Abs(dir.x) > MirrorDeadZone)
                    _sr.flipX = dir.x < 0f;
            }
            if (MovementOrDefault.RotateToVelocity && _currentSpeed > 0.01f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, angle), MovementOrDefault.TurnSpeed * deltaTime);
            }
        }

        private void Update()
        {
            if (UseMovementJobs && MovementJobSystem.IsActive)
                return;
            if (!destination.HasValue)
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, (MovementOrDefault.Deceleration) * Time.deltaTime);
                return;
            }

            var target = destination.Value;
            var pos = transform.position;
            var to = target - pos; to.z = 0f;
            float dist = to.magnitude;
            var m = MovementOrDefault;

            // Arrival check
            if (dist <= m.StopDistance)
            {
                transform.position = target;
                destination = null;
                _currentSpeed = 0f;
                return;
            }

            // Desired speed with slowdown near target
            float desiredSpeed = m.MaxSpeed;
            if (dist < m.SlowdownDistance)
                desiredSpeed = Mathf.Lerp(0.5f, m.MaxSpeed, dist / m.SlowdownDistance);
            float accel = desiredSpeed > _currentSpeed ? m.Acceleration : m.Deceleration;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, desiredSpeed, accel * Time.deltaTime);

            // Move
            Vector3 dir = to / dist;
            if (UseSteering && _steeringFrame == Time.frameCount)
            {
                Vector3 steered = dir + (_steering * SteeringInfluence);
                if (steered.sqrMagnitude > 0.0001f)
                    dir = steered.normalized;
            }
            Vector3 delta = dir * _currentSpeed * Time.deltaTime;
            if (delta.sqrMagnitude > to.sqrMagnitude)
                delta = to; // do not overshoot
            transform.position = pos + delta;

            ApplyFacing(dir, Time.deltaTime);
        }

        private void OnDrawGizmosSelected()
        {
            if (destination.HasValue)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, destination.Value);
            }
        }

        private MovementSettings MovementOrDefault
        {
            get
            {
                if (Movement != null) return Movement;
                return MovementSettings.Default;
            }
        }
    }
}
