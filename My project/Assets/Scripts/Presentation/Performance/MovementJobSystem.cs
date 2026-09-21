using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM]
// Logical block: Scripts/Presentation/Performance/MovementJobSystem.

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Job-based movement update for UnitView to reduce per-unit Update overhead.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public partial class MovementJobSystem : MonoBehaviour
    {
        // [MJOB-01]
        // Batch movement buffers and job-dispatch configuration for unit motion updates.
        public static MovementJobSystem Instance { get; private set; }
        public static bool IsActive => Instance != null && Instance.Enabled;

        [Tooltip("Enable job-based movement updates.")]
        public bool Enabled = true;
        [Tooltip("How often to update movement (seconds). 0 = every frame.")]
        public float Interval = 0f;
        [Tooltip("Job batch size.")]
        public int BatchSize = 32;
        [Tooltip("If true, updates are driven externally.")]
        public bool ExternalUpdate = false;

        private readonly List<UnitView> _units = new List<UnitView>(512);
        private Buffer _buffer;
        private JobHandle _jobHandle;
        private bool _jobActive;
        private float _timer;
        private float _lastDeltaTime;

        private struct Buffer
        {
            public NativeArray<float3> Positions;
            public NativeArray<float3> Destinations;
            public NativeArray<byte> HasDestination;
            public NativeArray<float> Speeds;
            public NativeArray<float> MaxSpeed;
            public NativeArray<float> Accel;
            public NativeArray<float> Decel;
            public NativeArray<float> Slowdown;
            public NativeArray<float> StopDist;
            public NativeArray<float3> Steering;
            public NativeArray<float3> OverrideVelocity;
            public NativeArray<byte> HasOverride;
            public NativeArray<float3> LastDirs;
            public NativeArray<float3> Directions;
            public NativeArray<byte> Arrived;
            public int Capacity;
            public int Count;
        }

        // [MJOB-02]
        // Lifecycle setup and persistent buffer allocation.
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_jobActive)
            {
                _jobHandle.Complete();
                _jobActive = false;
            }
            DisposeBuffer(ref _buffer);
            if (Instance == this) Instance = null;
        }

        // [MJOB-03]
        // Gather/apply movement state and schedule the per-unit movement job.
        private void Update()
        {
            if (ExternalUpdate) return;
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (!Enabled)
            {
                if (_jobActive)
                {
                    _jobHandle.Complete();
                    _jobActive = false;
                }
                return;
            }

            if (_jobActive && _jobHandle.IsCompleted)
            {
                _jobHandle.Complete();
                ApplyResults();
                _jobActive = false;
            }

            if (_jobActive) return;

            if (Interval > 0f)
            {
                _timer -= deltaTime;
                if (_timer > 0f) return;
                _timer = Interval;
            }

            int count = GatherUnits();
            if (count <= 0) return;

            EnsureCapacity(ref _buffer, count);
            if (!FillArrays(ref _buffer, count))
                return;

            var job = new MovementJob
            {
                Positions = _buffer.Positions,
                Destinations = _buffer.Destinations,
                HasDestination = _buffer.HasDestination,
                Speeds = _buffer.Speeds,
                MaxSpeed = _buffer.MaxSpeed,
                Accel = _buffer.Accel,
                Decel = _buffer.Decel,
                Slowdown = _buffer.Slowdown,
                StopDist = _buffer.StopDist,
                Steering = _buffer.Steering,
                OverrideVelocity = _buffer.OverrideVelocity,
                HasOverride = _buffer.HasOverride,
                LastDirs = _buffer.LastDirs,
                Directions = _buffer.Directions,
                Arrived = _buffer.Arrived,
                DeltaTime = deltaTime
            };

            _lastDeltaTime = deltaTime;
            _jobHandle = job.Schedule(count, Mathf.Max(1, BatchSize));
            _jobActive = true;
            _buffer.Count = count;
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("MovementJobSystem");
            go.AddComponent<MovementJobSystem>();
        }
    }
}
