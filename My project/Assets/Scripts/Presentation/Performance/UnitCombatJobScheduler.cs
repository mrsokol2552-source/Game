/*
@file: My project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.cs
@module: presentation.combat.jobs
@purpose: Main scheduler state, lifecycle, and dispatch timing for nearest-enemy combat jobs.
@entry: UCJS-01, UCJS-02
@api: UnitCombatJobScheduler singleton and scheduling loop
@deps: UnitCombat, UnitSoARegistry, Unity Jobs
@data: double-buffered unit lists, Native job-buffer ownership, scheduler timers
@perf: hotpath; runs every Interval and gates nearest-target recomputation cadence
@thread: main thread orchestration, worker-thread job scheduling
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs
@config: Interval, HashCellSize, HashRings, Disabled, UseSoARegistry
@assets: none
@notes: buffer management and job logic are extracted into sibling partial files
*/

using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Game.Presentation.View;
// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-UNITCOMBATJOBSCHEDULER]
// Logical block: Scripts/Presentation/Performance/UnitCombatJobScheduler.

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Periodically computes nearest enemy for all UnitCombat instances using a job.
    /// Uses a spatial hash to avoid O(N^2) scans on large crowds.
    /// </summary>
    public partial class UnitCombatJobScheduler : MonoBehaviour
    {
        // [UCJS-01]
        // Scheduler config, double-buffered unit lists, and Native job-buffer ownership.
        public static UnitCombatJobScheduler Instance { get; private set; }

        [Tooltip("How often to recompute nearest enemies for all units.")]
        public float Interval = 0.2f;
        [Tooltip("World cell size for spatial hash when searching nearest enemies.")]
        public float HashCellSize = 3.0f;
        [Tooltip("How many neighbor rings of hash cells to inspect when searching.")]
        public int HashRings = 1;
        [Tooltip("Disable job search (falls back to direct search in combat).")]
        public bool Disabled = false;
        [Tooltip("Use UnitSoARegistry snapshot for input data when available.")]
        public bool UseSoARegistry = true;

        private float _timer;
        private readonly List<UnitCombat>[] _unitBuffers =
        {
            new List<UnitCombat>(128),
            new List<UnitCombat>(128)
        };
        private readonly List<int>[] _indexBuffers =
        {
            new List<int>(128),
            new List<int>(128)
        };

        private struct Buffer
        {
            public NativeArray<Vector3> Positions;
            public NativeArray<int> Factions;
            public NativeArray<int> Nearest;
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

        // [UCJS-02]
        // Lifecycle setup and per-interval nearest-enemy job scheduling.
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

        private void Update()
        {
            if (_jobActive && _jobHandle.IsCompleted)
            {
                _jobHandle.Complete();
                ApplyResults(_activeBuffer);
                _jobActive = false;
            }

            _timer -= Time.deltaTime;
            if (_jobActive) return;
            if (_timer > 0f) return;
            _timer = Interval;
            if (Disabled) return;

            int nextBuffer = _activeBuffer == 0 ? 1 : 0;
            var units = _unitBuffers[nextBuffer];
            var indices = _indexBuffers[nextBuffer];
            int count;
            UnitSoARegistry.CombatSnapshot snapshot = default;
            bool usingSnapshot = false;
            var registry = UnitSoARegistry.Instance;
            if (UseSoARegistry && registry != null && registry.TryGetCombatSnapshot(out snapshot))
            {
                units.Clear();
                indices.Clear();
                int total = snapshot.Count;
                if (units.Capacity < total) units.Capacity = total;
                if (indices.Capacity < total) indices.Capacity = total;

                if (!snapshot.Positions.IsCreated || !snapshot.Factions.IsCreated ||
                    !snapshot.HasCombat.IsCreated || !snapshot.IsInSquad.IsCreated)
                {
                    count = GatherUnits(units);
                }
                else
                {
                    for (int i = 0; i < total; i++)
                    {
                        if (snapshot.HasCombat[i] == 0) continue;
                        if (snapshot.IsInSquad[i] != 0) continue;
                        var uc = snapshot.Units[i];
                        if (uc == null || !uc.isActiveAndEnabled) continue;
                        units.Add(uc);
                        indices.Add(i);
                    }
                    count = units.Count;
                    usingSnapshot = count > 0;
                }
            }
            else
            {
                indices.Clear();
                count = GatherUnits(units);
            }
            if (count <= 1) return;

            ref var buf = ref _buffers[nextBuffer];
            EnsureCapacity(ref buf, count);
            if (usingSnapshot)
            {
                if (!FillArraysFromSnapshot(ref buf, snapshot, indices, count)) return;
            }
            else
            {
                if (!FillArrays(ref buf, units, count)) return;
            }

            var job = new NearestEnemyJob
            {
                Positions = buf.Positions,
                Factions = buf.Factions,
                Nearest = buf.Nearest,
                Cells = buf.Cells,
                Buckets = buf.Buckets.AsReadOnly(),
                Rings = Mathf.Max(0, HashRings)
            };

            _jobHandle = job.Schedule(count, 32);
            _jobActive = true;
            _activeBuffer = nextBuffer;
            buf.Count = count;
        }

        private int GatherUnits(List<UnitCombat> target)
        {
            target.Clear();
            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                if (uc.IsInSquad) continue;
                target.Add(uc);
            }
            return target.Count;
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("UnitCombatJobScheduler");
            go.AddComponent<UnitCombatJobScheduler>();
        }
    }
}
