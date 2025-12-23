using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Game.Presentation.View;
namespace Game.Presentation.Performance
{
    /// <summary>
    /// Periodically computes nearest enemy for all UnitCombat instances using a job.
    /// Uses a spatial hash to avoid O(N^2) scans on large crowds.
    /// </summary>
    public class UnitCombatJobScheduler : MonoBehaviour
    {
        public static UnitCombatJobScheduler Instance { get; private set; }

        [Tooltip("How often to recompute nearest enemies for all units.")]
        public float Interval = 0.2f;
        [Tooltip("World cell size for spatial hash when searching nearest enemies.")]
        public float HashCellSize = 3.0f;
        [Tooltip("How many neighbor rings of hash cells to inspect when searching.")]
        public int HashRings = 1;
        [Tooltip("Disable job search (falls back to direct search in combat).")]
        public bool Disabled = false;

        private float _timer;
        private readonly List<UnitCombat> _units = new List<UnitCombat>(128);

        private NativeArray<Vector3> _positions;
        private NativeArray<int> _factions;
        private NativeArray<int> _nearest;
        private NativeArray<int2> _cells;
        private NativeParallelMultiHashMap<int, int> _buckets;
        private int _bucketCapacity;
        private int _capacity;

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
            DisposeArrays();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Interval;
            if (Disabled) return;

            int count = GatherUnits();
            if (count <= 1) return;

            EnsureCapacity(count);
            if (!FillArrays(count)) return;

            var job = new NearestEnemyJob
            {
                Positions = _positions,
                Factions = _factions,
                Nearest = _nearest,
                Cells = _cells,
                Buckets = _buckets.AsReadOnly(),
                Rings = Mathf.Max(0, HashRings)
            };

            var handle = job.Schedule(count, 32);
            handle.Complete();

            ApplyResults(count);
        }

        private int GatherUnits()
        {
            _units.Clear();
            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                // Optionally skip if occupancy hash will handle nearest; keep for target selection only
                _units.Add(uc);
            }
            return _units.Count;
        }

        private void EnsureCapacity(int count)
        {
            if (count <= _capacity && _buckets.IsCreated && _bucketCapacity >= count * 2) return;
            DisposeArrays();
            _capacity = Mathf.NextPowerOfTwo(count);
            _positions = new NativeArray<Vector3>(_capacity, Allocator.Persistent);
            _factions = new NativeArray<int>(_capacity, Allocator.Persistent);
            _nearest = new NativeArray<int>(_capacity, Allocator.Persistent);
            _cells = new NativeArray<int2>(_capacity, Allocator.Persistent);
            _bucketCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, count * 2));
            _buckets = new NativeParallelMultiHashMap<int, int>(_bucketCapacity, Allocator.Persistent);
        }

        private bool FillArrays(int count)
        {
            if (!_positions.IsCreated || !_factions.IsCreated || !_nearest.IsCreated || !_cells.IsCreated || !_buckets.IsCreated) return false;
            _buckets.Clear();
            for (int i = 0; i < count; i++)
            {
                var uc = _units[i];
                var pos = uc != null ? uc.transform.position : Vector3.zero;
                _positions[i] = pos;
                _factions[i] = uc != null ? (int)uc.Faction : -1;
                _nearest[i] = -1;
                var cell = ToCell(pos, HashCellSize);
                _cells[i] = cell;
                _buckets.Add(HashKey(cell.x, cell.y), i);
            }
            return true;
        }

        private void ApplyResults(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var uc = _units[i];
                if (uc == null) continue;
                int idx = _nearest[i];
                UnitCombat target = (idx >= 0 && idx < count) ? _units[idx] : null;
                uc.SetJobNearest(target);
            }
        }

        private void DisposeArrays()
        {
            if (_positions.IsCreated) { _positions.Dispose(); _positions = default; }
            if (_factions.IsCreated) { _factions.Dispose(); _factions = default; }
            if (_nearest.IsCreated) { _nearest.Dispose(); _nearest = default; }
            if (_cells.IsCreated) { _cells.Dispose(); _cells = default; }
            if (_buckets.IsCreated) { _buckets.Dispose(); _buckets = default; }
            _capacity = 0;
            _bucketCapacity = 0;
        }

        private struct NearestEnemyJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> Positions;
            [ReadOnly] public NativeArray<int> Factions;
            [WriteOnly] public NativeArray<int> Nearest;
            [ReadOnly] public NativeArray<int2> Cells;
            [ReadOnly] public NativeParallelMultiHashMap<int, int>.ReadOnly Buckets;
            [ReadOnly] public int Rings;

            public void Execute(int index)
            {
                var p = Positions[index];
                int f = Factions[index];
                float best = float.MaxValue;
                int bestIdx = -1;
                var myCell = Cells[index];
                int rings = Mathf.Max(0, Rings);

                for (int dy = -rings; dy <= rings; dy++)
                {
                    for (int dx = -rings; dx <= rings; dx++)
                    {
                        int key = HashKey(myCell.x + dx, myCell.y + dy);
                        if (!Buckets.TryGetFirstValue(key, out var otherIdx, out var it))
                            continue;
                        do
                        {
                            if (otherIdx == index) continue;
                            if (Factions[otherIdx] == f) continue;
                            float d2 = (Positions[otherIdx] - p).sqrMagnitude;
                            if (d2 < best)
                            {
                                best = d2;
                                bestIdx = otherIdx;
                            }
                        }
                        while (Buckets.TryGetNextValue(out otherIdx, ref it));
                    }
                }
                Nearest[index] = bestIdx;
            }

            private static int HashKey(int x, int y)
            {
                unchecked
                {
                    int h = 73856093 ^ x;
                    h = (h * 19349663) ^ y;
                    return h;
                }
            }
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("UnitCombatJobScheduler");
            go.AddComponent<UnitCombatJobScheduler>();
        }

        private static int HashKey(int x, int y)
        {
            unchecked
            {
                int h = 73856093 ^ x;
                h = (h * 19349663) ^ y;
                return h;
            }
        }

        private static int2 ToCell(Vector3 pos, float cellSize)
        {
            float inv = cellSize > 0.0001f ? 1f / cellSize : 1f;
            int x = Mathf.FloorToInt(pos.x * inv);
            int y = Mathf.FloorToInt(pos.y * inv);
            return new int2(x, y);
        }
    }
}
