using UnityEngine;

namespace Game.Presentation.View
{
    [CreateAssetMenu(fileName = "MovementSettings", menuName = "Game/Movement Settings", order = 0)]
    public class MovementSettings : ScriptableObject
    {
        [Header("Speed")]
        public float MaxSpeed = 2f;
        [Tooltip("Acceleration towards target speed (units/sec^2).")]
        public float Acceleration = 6f;
        [Tooltip("Deceleration when slowing down or stopping (units/sec^2).")]
        public float Deceleration = 10f;

        [Header("Arrival")]
        [Tooltip("Distance at which unit starts slowing down.")]
        public float SlowdownDistance = 0.3f;
        [Tooltip("Distance at which unit snaps to target and stops.")]
        public float StopDistance = 0.05f;

        [Header("Facing")]
        public bool RotateToVelocity = false;
        [Tooltip("Max turn speed in degrees/second if RotateToVelocity is true.")]
        public float TurnSpeed = 540f;

        private static MovementSettings _default;
        public static MovementSettings Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<MovementSettings>();
                }
                return _default;
            }
        }
    }
}
