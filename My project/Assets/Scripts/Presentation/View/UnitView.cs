using Game.Domain.Units;
using UnityEngine;

namespace Game.Presentation.View
{
    public class UnitView : MonoBehaviour
    {
        public UnitStats Stats = new UnitStats();
        [Header("Movement")]
        public MovementSettings Movement; // optional; if null uses defaults
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
        public static bool EnableJitterLog = false;

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

        private void Update()
        {
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
            _lastDir = dir;
            Vector3 delta = dir * _currentSpeed * Time.deltaTime;
            if (delta.sqrMagnitude > to.sqrMagnitude)
                delta = to; // do not overshoot
            transform.position = pos + delta;

            // Flip sprite instead of rotating
            if (MirrorSpriteX && _sr != null)
            {
                if (Mathf.Abs(dir.x) > MirrorDeadZone)
                    _sr.flipX = dir.x < 0f;
            }

            // Optional facing
            if (m.RotateToVelocity && _currentSpeed > 0.01f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, angle), m.TurnSpeed * Time.deltaTime);
            }
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
