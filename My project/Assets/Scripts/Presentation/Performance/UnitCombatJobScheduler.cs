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
        private readonly List<UnitCombat>[] _unitBuffers =
        {
            new List<UnitCombat>(128),
            new List<UnitCombat>(128)
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
            int count = GatherUnits(units);
            if (count <= 1) return;

            ref var buf = ref _buffers[nextBuffer];
            EnsureCapacity(ref buf, count);
            if (!FillArrays(ref buf, units, count)) return;

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
                target.Add(uc);
            }
            return target.Count;
        }

        private void EnsureCapacity(ref Buffer buf, int count)
        {
            if (count <= buf.Capacity && buf.Buckets.IsCreated && buf.BucketCapacity >= count * 2) return;
            DisposeBuffer(ref buf);
            buf.Capacity = Mathf.NextPowerOfTwo(count);
            buf.Positions = new NativeArray<Vector3>(buf.Capacity, Allocator.Persistent);
            buf.Factions = new NativeArray<int>(buf.Capacity, Allocator.Persistent);
            buf.Nearest = new NativeArray<int>(buf.Capacity, Allocator.Persistent);
            buf.Cells = new NativeArray<int2>(buf.Capacity, Allocator.Persistent);
            buf.BucketCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, count * 2));
            buf.Buckets = new NativeParallelMultiHashMap<int, int>(buf.BucketCapacity, Allocator.Persistent);
        }

        private bool FillArrays(ref Buffer buf, List<UnitCombat> units, int count)
        {
            if (!buf.Positions.IsCreated || !buf.Factions.IsCreated || !buf.Nearest.IsCreated || !buf.Cells.IsCreated || !buf.Buckets.IsCreated)
                return false;
            buf.Buckets.Clear();
            for (int i = 0; i < count; i++)
            {
                var uc = units[i];
                var pos = uc != null ? uc.transform.position : Vector3.zero;
                buf.Positions[i] = pos;
                buf.Factions[i] = uc != null ? (int)uc.Faction : -1;
                buf.Nearest[i] = -1;
                var cell = ToCell(pos, HashCellSize);
                buf.Cells[i] = cell;
                buf.Buckets.Add(HashKey(cell.x, cell.y), i);
            }
            return true;
        }

        private void ApplyResults(int bufferIndex)
        {
            if (bufferIndex < 0 || bufferIndex >= _buffers.Length) return;
            ref var buf = ref _buffers[bufferIndex];
            int count = buf.Count;
            if (count <= 0) return;
            var units = _unitBuffers[bufferIndex];
            for (int i = 0; i < count; i++)
            {
                var uc = units[i];
                if (uc == null) continue;
                int idx = buf.Nearest[i];
                UnitCombat target = (idx >= 0 && idx < count) ? units[idx] : null;
                uc.SetJobNearest(target);
            }
            units.Clear();
        }

        private void DisposeBuffers()
        {
            for (int i = 0; i < _buffers.Length; i++)
            {
                DisposeBuffer(ref _buffers[i]);
            }
        }

        private static void DisposeBuffer(ref Buffer buf)
        {
            if (buf.Positions.IsCreated) { buf.Positions.Dispose(); buf.Positions = default; }
            if (buf.Factions.IsCreated) { buf.Factions.Dispose(); buf.Factions = default; }
            if (buf.Nearest.IsCreated) { buf.Nearest.Dispose(); buf.Nearest = default; }
            if (buf.Cells.IsCreated) { buf.Cells.Dispose(); buf.Cells = default; }
            if (buf.Buckets.IsCreated) { buf.Buckets.Dispose(); buf.Buckets = default; }
            buf.Capacity = 0;
            buf.BucketCapacity = 0;
            buf.Count = 0;
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
