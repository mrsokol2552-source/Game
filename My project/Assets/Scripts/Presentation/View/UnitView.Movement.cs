/*
@file: My project/Assets/Scripts/Presentation/View/UnitView.Movement.cs
@module: presentation.view.unit
@purpose: Extracted movement tick, steering blend, and facing logic for UnitView.
@entry: UVEW-03
@api: UnitView partial movement implementation
@deps: MovementSettings, MovementJobSystem
@data: destination, speed, last direction, steering overrides
@perf: hotpath; executed every frame for non-job-driven units
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual movement verification
@config: UseMovementJobs, UseSteering, SteeringInfluence, facing settings
@assets: SpriteRenderer on the unit GameObject
@notes: this is the gameplay-movement seam that should remain separate from render-only concerns
*/

using Game.Presentation.Performance;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITVIEW-MOVEMENT]
// Logical block: UnitView movement extraction.

namespace Game.Presentation.View
{
    public partial class UnitView
    {
        // [UVEW-03]
        // Per-frame movement integration, steering blend, arrival handling, and facing updates.
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
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.Euler(0f, 0f, angle),
                    MovementOrDefault.TurnSpeed * deltaTime);
            }
        }

        private void Update()
        {
            if (UseMovementJobs && MovementJobSystem.IsActive)
                return;

            if (!destination.HasValue)
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, MovementOrDefault.Deceleration * Time.deltaTime);
                return;
            }

            Vector3 target = destination.Value;
            Vector3 pos = transform.position;
            Vector3 to = target - pos;
            to.z = 0f;
            float dist = to.magnitude;
            MovementSettings movement = MovementOrDefault;

            if (dist <= movement.StopDistance)
            {
                transform.position = target;
                destination = null;
                _currentSpeed = 0f;
                return;
            }

            float desiredSpeed = movement.MaxSpeed;
            if (dist < movement.SlowdownDistance)
                desiredSpeed = Mathf.Lerp(0.5f, movement.MaxSpeed, dist / movement.SlowdownDistance);

            float accel = desiredSpeed > _currentSpeed ? movement.Acceleration : movement.Deceleration;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, desiredSpeed, accel * Time.deltaTime);

            Vector3 dir = to / dist;
            if (UseSteering && _steeringFrame == Time.frameCount)
            {
                Vector3 steered = dir + (_steering * SteeringInfluence);
                if (steered.sqrMagnitude > 0.0001f)
                    dir = steered.normalized;
            }

            Vector3 delta = dir * _currentSpeed * Time.deltaTime;
            if (delta.sqrMagnitude > to.sqrMagnitude)
                delta = to;
            transform.position = pos + delta;

            ApplyFacing(dir, Time.deltaTime);
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
