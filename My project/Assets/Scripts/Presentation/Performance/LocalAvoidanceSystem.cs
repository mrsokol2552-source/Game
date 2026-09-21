/*
@file: My project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.cs
@module: presentation.performance
@purpose: Schedules the legacy lightweight local-avoidance pass built on a spatial hash and a simple steering job.
@entry: LocalAvoidanceSystem.Update, LAVO-02
@api: singleton MonoBehaviour scheduler
@deps: UnitCombat, UnitView, OrcaAvoidanceSystem, NativeArray buffers, Unity Jobs
@data: double-buffered unit snapshots and steering outputs for the next frame
@perf: medium hotpath; active only when ORCA is disabled
@thread: main thread + Unity job workers
@tests: covered indirectly by repo audits and runtime movement regressions
@config: interval, cell size, avoid radius, neighbor rings, and batch size
@notes: treated as a legacy fallback; CompositionRoot disables it whenever ORCA is active
*/

using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-LOCALAVOIDANCESYSTEM]
// Logical block: Scripts/Presentation/Performance/LocalAvoidanceSystem.

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Computes lightweight local avoidance steering using a spatial hash + job.
    /// Feeds steering vectors into UnitView for the next frame.
    /// </summary>
    public partial class LocalAvoidanceSystem : MonoBehaviour
    {
        // [LAVO-01]
        // Legacy local-avoidance config, singleton ownership, and double-buffered runtime state.
        public static LocalAvoidanceSystem Instance { get; private set; }

        [Tooltip("Enable local avoidance steering.")]
        public bool Enabled = true;
        [Tooltip("How often to recompute steering (seconds). 0 = every frame.")]
        public float Interval = 0.05f;
        [Tooltip("World cell size for avoidance spatial hash.")]
        public float CellSize = 1.5f;
        [Tooltip("Avoidance radius in world units.")]
        public float AvoidRadius = 0.8f;
        [Tooltip("Scale of avoidance steering.")]
        public float AvoidStrength = 1f;
        [Tooltip("Clamp magnitude of steering vector.")]
        public float MaxSteer = 1.2f;
        [Tooltip("Neighbor rings to scan; 0 = derive from radius.")]
        public int Rings = 0;
        [Tooltip("Max neighbors considered per unit (0 = unlimited).")]
        public int MaxNeighbors = 12;
        [Tooltip("Avoid enemies as well as friendlies.")]
        public bool AvoidEnemies = false;
        [Tooltip("Skip units that have no destination.")]
        public bool SkipWithoutDestination = true;
        [Tooltip("Batch size for the job scheduler.")]
        public int BatchSize = 32;

        private float _timer;
        private readonly List<UnitCombat>[] _unitBuffers =
        {
            new List<UnitCombat>(256),
            new List<UnitCombat>(256)
        };

        private struct Buffer
        {
            public NativeArray<float3> Positions;
            public NativeArray<byte> HasDest;
            public NativeArray<int> Factions;
            public NativeArray<float3> Steering;
            public NativeArray<int2> Cells;
            public NativeParallelMultiHashMap<int, int> Buckets;
            public int Capacity;
            public int BucketCapacity;
            public int Count;
        }

        private readonly Buffer[] _buffers = new Buffer[2];
        private int _activeBuffer = -1;
        private JobHandle _jobHandle;
        private bool _jobActive;

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
            DisposeBuffers();
            if (Instance == this) Instance = null;
        }

        // [LAVO-02]
        // Scheduler tick, ORCA handoff, job completion, and next avoidance job submission.
        private void Update()
        {
            if (OrcaAvoidanceSystem.IsActive)
            {
                if (_jobActive)
                {
                    _jobHandle.Complete();
                    _jobActive = false;
                }
                return;
            }
            if (!Enabled || AvoidRadius <= 0.0001f || CellSize <= 0.0001f)
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
                ApplyResults(_activeBuffer);
                _jobActive = false;
            }

            if (_jobActive) return;

            if (Interval > 0f)
            {
                _timer -= Time.deltaTime;
                if (_timer > 0f) return;
                _timer = Interval;
            }

            int nextBuffer = _activeBuffer == 0 ? 1 : 0;
            var units = _unitBuffers[nextBuffer];
            int count = GatherUnits(units);
            if (count <= 0) return;

            ref var buf = ref _buffers[nextBuffer];
            EnsureCapacity(ref buf, count);
            if (!FillArrays(ref buf, units, count))
                return;

            int rings = Rings > 0 ? Rings : Mathf.Max(1, Mathf.CeilToInt(AvoidRadius / CellSize));
            float radiusSq = AvoidRadius * AvoidRadius;
            float invRadiusSq = radiusSq > 0.0001f ? 1f / radiusSq : 0f;

            var job = new AvoidanceJob
            {
                Positions = buf.Positions,
                HasDest = buf.HasDest,
                Factions = buf.Factions,
                Steering = buf.Steering,
                Cells = buf.Cells,
                Buckets = buf.Buckets.AsReadOnly(),
                RadiusSq = radiusSq,
                InvRadiusSq = invRadiusSq,
                Strength = Mathf.Max(0f, AvoidStrength),
                MaxSteer = Mathf.Max(0f, MaxSteer),
                Rings = Mathf.Max(0, rings),
                MaxNeighbors = MaxNeighbors,
                AvoidEnemies = AvoidEnemies,
                SkipWithoutDestination = SkipWithoutDestination
            };

            _jobHandle = job.Schedule(count, Mathf.Max(1, BatchSize));
            _jobActive = true;
            _activeBuffer = nextBuffer;
            buf.Count = count;
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("LocalAvoidanceSystem");
            go.AddComponent<LocalAvoidanceSystem>();
        }
    }
}
