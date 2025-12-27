using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Computes lightweight local avoidance steering using a spatial hash + job.
    /// Feeds steering vectors into UnitView for the next frame.
    /// </summary>
    public class LocalAvoidanceSystem : MonoBehaviour
    {
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

        private void Update()
        {
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
            buf.Capacity = Mathf.NextPowerOfTwo(Mathf.Max(4, count));
            buf.Positions = new NativeArray<float3>(buf.Capacity, Allocator.Persistent);
            buf.HasDest = new NativeArray<byte>(buf.Capacity, Allocator.Persistent);
            buf.Factions = new NativeArray<int>(buf.Capacity, Allocator.Persistent);
            buf.Steering = new NativeArray<float3>(buf.Capacity, Allocator.Persistent);
            buf.Cells = new NativeArray<int2>(buf.Capacity, Allocator.Persistent);
            buf.BucketCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, count * 2));
            buf.Buckets = new NativeParallelMultiHashMap<int, int>(buf.BucketCapacity, Allocator.Persistent);
        }

        private bool FillArrays(ref Buffer buf, List<UnitCombat> units, int count)
        {
            if (!buf.Positions.IsCreated || !buf.HasDest.IsCreated || !buf.Factions.IsCreated || !buf.Steering.IsCreated || !buf.Cells.IsCreated || !buf.Buckets.IsCreated)
                return false;

            buf.Buckets.Clear();
            for (int i = 0; i < count; i++)
            {
                var uc = units[i];
                var view = uc != null ? uc.GetComponent<UnitView>() : null;
                Vector3 pos = view != null ? view.transform.position : (uc != null ? uc.transform.position : Vector3.zero);
                buf.Positions[i] = pos;
                buf.Factions[i] = uc != null ? (int)uc.Faction : -1;

                byte hasDest = 0;
                if (view != null && view.TryGetDestination(out _))
                    hasDest = 1;
                buf.HasDest[i] = hasDest;

                var cell = ToCell(pos, CellSize);
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
            int applyFrame = Time.frameCount + 1;
            for (int i = 0; i < count; i++)
            {
                var uc = units[i];
                if (uc == null) continue;
                var view = uc.GetComponent<UnitView>();
                if (view == null) continue;
                view.SetSteering(buf.Steering[i], applyFrame);
            }
            units.Clear();
        }

        private void DisposeBuffers()
        {
            for (int i = 0; i < _buffers.Length; i++)
                DisposeBuffer(ref _buffers[i]);
        }

        private static void DisposeBuffer(ref Buffer buf)
        {
            if (buf.Positions.IsCreated) { buf.Positions.Dispose(); buf.Positions = default; }
            if (buf.HasDest.IsCreated) { buf.HasDest.Dispose(); buf.HasDest = default; }
            if (buf.Factions.IsCreated) { buf.Factions.Dispose(); buf.Factions = default; }
            if (buf.Steering.IsCreated) { buf.Steering.Dispose(); buf.Steering = default; }
            if (buf.Cells.IsCreated) { buf.Cells.Dispose(); buf.Cells = default; }
            if (buf.Buckets.IsCreated) { buf.Buckets.Dispose(); buf.Buckets = default; }
            buf.Capacity = 0;
            buf.BucketCapacity = 0;
            buf.Count = 0;
        }

        private struct AvoidanceJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<byte> HasDest;
            [ReadOnly] public NativeArray<int> Factions;
            [WriteOnly] public NativeArray<float3> Steering;
            [ReadOnly] public NativeArray<int2> Cells;
            [ReadOnly] public NativeParallelMultiHashMap<int, int>.ReadOnly Buckets;
            [ReadOnly] public float RadiusSq;
            [ReadOnly] public float InvRadiusSq;
            [ReadOnly] public float Strength;
            [ReadOnly] public float MaxSteer;
            [ReadOnly] public int Rings;
            [ReadOnly] public int MaxNeighbors;
            [ReadOnly] public bool AvoidEnemies;
            [ReadOnly] public bool SkipWithoutDestination;

            public void Execute(int index)
            {
                if (SkipWithoutDestination && HasDest[index] == 0)
                {
                    Steering[index] = default;
                    return;
                }

                float3 pos = Positions[index];
                float3 sum = default;
                int neighbors = 0;
                int f = Factions[index];
                var cell = Cells[index];
                bool stop = false;

                for (int dy = -Rings; dy <= Rings && !stop; dy++)
                {
                    for (int dx = -Rings; dx <= Rings && !stop; dx++)
                    {
                        int key = HashKey(cell.x + dx, cell.y + dy);
                        if (!Buckets.TryGetFirstValue(key, out var otherIdx, out var it))
                            continue;
                        do
                        {
                            if (otherIdx == index) continue;
                            if (!AvoidEnemies && Factions[otherIdx] != f) continue;

                            float3 delta = pos - Positions[otherIdx];
                            float dist2 = math.lengthsq(delta);
                            if (dist2 <= 0.0001f || dist2 > RadiusSq) continue;
                            float weight = 1f - (dist2 * InvRadiusSq);
                            if (weight <= 0f) continue;

                            float inv = math.rsqrt(dist2);
                            sum += delta * inv * weight;

                            if (MaxNeighbors > 0 && ++neighbors >= MaxNeighbors)
                            {
                                stop = true;
                                break;
                            }
                        }
                        while (Buckets.TryGetNextValue(out otherIdx, ref it));
                    }
                }

                float mag = math.length(sum);
                if (mag > 0.0001f)
                {
                    float3 dir = sum / mag;
                    float scaled = math.min(MaxSteer, mag * Strength);
                    Steering[index] = dir * scaled;
                }
                else
                {
                    Steering[index] = default;
                }
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
            var go = new GameObject("LocalAvoidanceSystem");
            go.AddComponent<LocalAvoidanceSystem>();
        }

        private static int2 ToCell(Vector3 pos, float cellSize)
        {
            float inv = cellSize > 0.0001f ? 1f / cellSize : 1f;
            int x = Mathf.FloorToInt(pos.x * inv);
            int y = Mathf.FloorToInt(pos.y * inv);
            return new int2(x, y);
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
}
