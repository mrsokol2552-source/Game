/*
@file: My project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.cs
@module: presentation.movement.orca
@purpose: Computes ORCA/RVO local avoidance velocity overrides using spatial hashing and jobs.
@entry: OrcaAvoidanceSystem.Update, ORCA-03, ORCA-04
@api: shared MonoBehaviour singleton queried by movement/combat systems
@deps: UnitCombat, UnitSoARegistry, MovementJobSystem, Unity Jobs/Collections
@data: agent snapshots, cell hash buffers, avoidance outputs, NativeCollections
@perf: hotpath, large-N avoidance system; radius/neighbor settings heavily affect cost
@thread: main thread scheduler + worker jobs
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual crowd movement verification
@config: ORCA inspector settings, docs/runtime_switches.md
@assets: none directly
@notes: ORCA should not fight squad/flow decisions; disable or relax it in states that intentionally ignore local steering
*/

using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM]
// Logical block: Scripts/Presentation/Performance/OrcaAvoidanceSystem.

namespace Game.Presentation.Performance
{
    /// <summary>
    /// ORCA/RVO local avoidance system using a spatial hash + job.
    /// Produces per-unit velocity overrides for the next frame.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public partial class OrcaAvoidanceSystem : MonoBehaviour
    {
        // [ORCA-01]
        // ORCA system parameters, spatial buffers, and shared NativeCollections for avoidance jobs.
        public static OrcaAvoidanceSystem Instance { get; private set; }
        public static bool IsActive => Instance != null && Instance.Enabled;

        [Tooltip("Enable ORCA/RVO avoidance.")]
        public bool Enabled = true;
        [Tooltip("How often to recompute ORCA (seconds). 0 = every frame.")]
        public float Interval = 0f;
        [Tooltip("If true, updates are driven externally.")]
        public bool ExternalUpdate = false;
        [Tooltip("World cell size for spatial hash.")]
        public float CellSize = 1.5f;
        [Tooltip("Neighbor search radius in world units.")]
        public float NeighborDist = 2.5f;
        [Tooltip("Max neighbors considered per unit (higher = more stable but slower).")]
        public int MaxNeighbors = 12;
        [Tooltip("Agent radius in world units.")]
        public float AgentRadius = 0.35f;
        [Header("Auto Radius")]
        [Tooltip("If true, auto-tunes AgentRadius from the first active unit's sprite size.")]
        public bool AutoAgentRadiusFromSprite = true;
        [Tooltip("Scale applied to sprite extents when auto-tuning (0.5 = half width).")]
        public float AutoAgentRadiusScale = 0.6f;
        [Tooltip("Minimum allowed auto radius.")]
        public float AutoAgentRadiusMin = 0.25f;
        [Tooltip("NeighborDist = AgentRadius * this (when auto-tuned).")]
        public float AutoNeighborDistScale = 4f;
        [Tooltip("CellSize = AgentRadius * this (when auto-tuned).")]
        public float AutoCellSizeScale = 3f;
        [Tooltip("Time horizon (seconds) for collision avoidance.")]
        public float TimeHorizon = 1.5f;
        [Header("Cohesion")]
        [Tooltip("Bias preferred velocity toward local friendly centroid.")]
        public bool UseCohesion = true;
        [Tooltip("Radius to sample friendly centroid (world units).")]
        public float CohesionRadius = 4f;
        [Tooltip("Weight of cohesion contribution (0..1).")]
        public float CohesionWeight = 0.3f;
        [Tooltip("Max cohesion speed as a fraction of max speed.")]
        public float CohesionMaxSpeedFraction = 0.5f;
        [Tooltip("Avoid enemies as well as allies.")]
        public bool AvoidEnemies = true;
        [Tooltip("Skip units without destination (do not override).")]
        public bool SkipWithoutDestination = true;
        [Tooltip("Job batch size.")]
        public int BatchSize = 32;
        [Tooltip("Minimum responsibility weight for avoidance (prevents zero-weight agents).")]
        public float MinResponsibility = 0.05f;
        [Tooltip("Use UnitSoARegistry for input data when available.")]
        public bool UseSoARegistry = true;

        private readonly List<UnitView>[] _unitBuffers =
        {
            new List<UnitView>(256),
            new List<UnitView>(256)
        };

        private struct Line
        {
            public float2 point;
            public float2 direction;
        }

        private struct Buffer
        {
            public NativeArray<float2> Positions;
            public NativeArray<float2> Velocities;
            public NativeArray<float2> Preferred;
            public NativeArray<float> MaxSpeed;
            public NativeArray<byte> HasDestination;
            public NativeArray<byte> UseOrca;
            public NativeArray<float> Responsibility;
            public NativeArray<int> Factions;
            public NativeArray<float2> OutputVelocity;
            public NativeArray<int2> Cells;
            public NativeParallelMultiHashMap<int, int> Buckets;
            [NativeDisableParallelForRestriction] public NativeArray<Line> Lines;
            [NativeDisableParallelForRestriction] public NativeArray<Line> ScratchLines;
            public int Capacity;
            public int BucketCapacity;
            public int LineCapacity;
            public int MaxNeighbors;
            public int Count;
        }

        private readonly Buffer[] _buffers = new Buffer[2];
        private int _activeBuffer = -1;
        private JobHandle _jobHandle;
        private bool _jobActive;
        private float _timer;

        // [ORCA-02]
        // Lifecycle setup and buffer allocation.
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

        // [ORCA-03]
        // Frame update that gathers agents, builds the neighborhood hash, and dispatches ORCA jobs.
        private void Update()
        {
            if (ExternalUpdate) return;
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (_jobActive && _jobHandle.IsCompleted)
            {
                _jobHandle.Complete();
                ApplyResults(_activeBuffer);
                _jobActive = false;
            }

            if (!Enabled) return;
            if (_jobActive) return;

            MaybeAutoTuneFromUnits();

            if (Interval > 0f)
            {
                _timer -= deltaTime;
                if (_timer > 0f) return;
                _timer = Interval;
            }

            int nextBuffer = _activeBuffer == 0 ? 1 : 0;
            var units = _unitBuffers[nextBuffer];
            int count;
            UnitSoARegistry.OrcaSnapshot snapshot = default;
            bool usingSnapshot = false;
            var registry = UnitSoARegistry.Instance;
            if (registry != null)
            {
                registry.OrcaCellSize = CellSize;
                registry.OrcaMinResponsibility = MinResponsibility;
            }
            if (UseSoARegistry && registry != null && registry.TryGetOrcaSnapshot(out snapshot))
            {
                units.Clear();
                if (units.Capacity < snapshot.Count) units.Capacity = snapshot.Count;
                units.AddRange(snapshot.Units);
                count = snapshot.Count;
                usingSnapshot = count > 0;
            }
            else
            {
                count = GatherUnits(units);
            }
            if (count <= 0) return;

            int effectiveMaxNeighbors = Mathf.Max(1, MaxNeighbors);
            ref var buf = ref _buffers[nextBuffer];
            EnsureCapacity(ref buf, count, effectiveMaxNeighbors);
            if (usingSnapshot)
            {
                if (!FillArraysFromSnapshot(ref buf, snapshot, count))
                    return;
            }
            else
            {
                if (!FillArrays(ref buf, units, count))
                    return;
            }

            int rings = Mathf.Max(1, Mathf.CeilToInt(NeighborDist / Mathf.Max(0.0001f, CellSize)));
            var job = new OrcaJob
            {
                Positions = buf.Positions,
                Velocities = buf.Velocities,
                Preferred = buf.Preferred,
                MaxSpeed = buf.MaxSpeed,
                HasDestination = buf.HasDestination,
                UseOrca = buf.UseOrca,
                Responsibility = buf.Responsibility,
                Factions = buf.Factions,
                OutputVelocity = buf.OutputVelocity,
                Cells = buf.Cells,
                Buckets = buf.Buckets.AsReadOnly(),
                Lines = buf.Lines,
                ScratchLines = buf.ScratchLines,
                MaxNeighbors = effectiveMaxNeighbors,
                NeighborDistSq = NeighborDist * NeighborDist,
                AgentRadius = Mathf.Max(0.01f, AgentRadius),
                TimeHorizon = Mathf.Max(0.05f, TimeHorizon),
                DeltaTime = deltaTime,
                Rings = rings,
                UseCohesion = UseCohesion && CohesionWeight > 0f && CohesionRadius > 0f,
                CohesionRadiusSq = CohesionRadius * CohesionRadius,
                CohesionWeight = Mathf.Max(0f, CohesionWeight),
                CohesionMaxSpeedFraction = Mathf.Max(0f, CohesionMaxSpeedFraction),
                AvoidEnemies = AvoidEnemies,
                SkipWithoutDestination = SkipWithoutDestination
            };

            _jobHandle = job.Schedule(count, Mathf.Max(1, BatchSize));
            _jobActive = true;
            _activeBuffer = nextBuffer;
            buf.Count = count;
        }

        private void MaybeAutoTuneFromUnits()
        {
            if (!AutoAgentRadiusFromSprite) return;
            foreach (var unit in UnitView.All)
            {
                if (unit == null || !unit.isActiveAndEnabled) continue;
                var sr = unit.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null) continue;
                float extent = Mathf.Max(sr.bounds.extents.x, sr.bounds.extents.y);
                if (extent <= 0.0001f) continue;
                float radius = Mathf.Max(AutoAgentRadiusMin, extent * AutoAgentRadiusScale);
                AgentRadius = radius;
                NeighborDist = Mathf.Max(NeighborDist, radius * AutoNeighborDistScale);
                CellSize = Mathf.Max(CellSize, radius * AutoCellSizeScale);
                return;
            }
        }

        // [ORCA-04]
        // Parallel ORCA solver that computes velocity constraints and final avoidance vectors.
        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("OrcaAvoidanceSystem");
            go.AddComponent<OrcaAvoidanceSystem>();
        }
    }
}
