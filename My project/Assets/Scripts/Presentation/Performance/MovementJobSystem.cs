using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Job-based movement update for UnitView to reduce per-unit Update overhead.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MovementJobSystem : MonoBehaviour
    {
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

        private int GatherUnits()
        {
            _units.Clear();
            foreach (var uv in UnitView.All)
            {
                if (uv == null || !uv.isActiveAndEnabled) continue;
                if (!uv.UseMovementJobs) continue;
                _units.Add(uv);
            }
            return _units.Count;
        }

        private void EnsureCapacity(ref Buffer buf, int count)
        {
            if (count <= buf.Capacity) return;
            DisposeBuffer(ref buf);
            buf.Capacity = Mathf.NextPowerOfTwo(Mathf.Max(4, count));
            buf.Positions = new NativeArray<float3>(buf.Capacity, Allocator.Persistent);
            buf.Destinations = new NativeArray<float3>(buf.Capacity, Allocator.Persistent);
            buf.HasDestination = new NativeArray<byte>(buf.Capacity, Allocator.Persistent);
            buf.Speeds = new NativeArray<float>(buf.Capacity, Allocator.Persistent);
            buf.MaxSpeed = new NativeArray<float>(buf.Capacity, Allocator.Persistent);
            buf.Accel = new NativeArray<float>(buf.Capacity, Allocator.Persistent);
            buf.Decel = new NativeArray<float>(buf.Capacity, Allocator.Persistent);
            buf.Slowdown = new NativeArray<float>(buf.Capacity, Allocator.Persistent);
            buf.StopDist = new NativeArray<float>(buf.Capacity, Allocator.Persistent);
            buf.Steering = new NativeArray<float3>(buf.Capacity, Allocator.Persistent);
            buf.OverrideVelocity = new NativeArray<float3>(buf.Capacity, Allocator.Persistent);
            buf.HasOverride = new NativeArray<byte>(buf.Capacity, Allocator.Persistent);
            buf.LastDirs = new NativeArray<float3>(buf.Capacity, Allocator.Persistent);
            buf.Directions = new NativeArray<float3>(buf.Capacity, Allocator.Persistent);
            buf.Arrived = new NativeArray<byte>(buf.Capacity, Allocator.Persistent);
        }

        private bool FillArrays(ref Buffer buf, int count)
        {
            if (!buf.Positions.IsCreated || !buf.Destinations.IsCreated || !buf.HasDestination.IsCreated ||
                !buf.Speeds.IsCreated || !buf.MaxSpeed.IsCreated || !buf.Accel.IsCreated || !buf.Decel.IsCreated ||
                !buf.Slowdown.IsCreated || !buf.StopDist.IsCreated || !buf.Steering.IsCreated ||
                !buf.OverrideVelocity.IsCreated || !buf.HasOverride.IsCreated || !buf.LastDirs.IsCreated ||
                !buf.Directions.IsCreated || !buf.Arrived.IsCreated)
                return false;

            for (int i = 0; i < count; i++)
            {
                var uv = _units[i];
                var pos = uv.transform.position;
                buf.Positions[i] = pos;
                buf.Speeds[i] = uv.GetSpeed();

                var m = uv.GetMovementSettings();
                buf.MaxSpeed[i] = m.MaxSpeed;
                buf.Accel[i] = m.Acceleration;
                buf.Decel[i] = m.Deceleration;
                buf.Slowdown[i] = m.SlowdownDistance;
                buf.StopDist[i] = m.StopDistance;

                if (uv.TryGetDestination(out var dest))
                {
                    buf.HasDestination[i] = 1;
                    buf.Destinations[i] = dest;
                }
                else
                {
                    buf.HasDestination[i] = 0;
                    buf.Destinations[i] = pos;
                }

                if (uv.TryGetSteering(out var steer))
                    buf.Steering[i] = steer * uv.SteeringInfluence;
                else
                    buf.Steering[i] = default;

                if (uv.TryGetVelocityOverride(out var velocityOverride))
                {
                    buf.HasOverride[i] = 1;
                    buf.OverrideVelocity[i] = velocityOverride;
                }
                else
                {
                    buf.HasOverride[i] = 0;
                    buf.OverrideVelocity[i] = default;
                }

                buf.LastDirs[i] = uv.GetLastDirection();
                buf.Directions[i] = default;
                buf.Arrived[i] = 0;
            }
            return true;
        }

        private void ApplyResults()
        {
            int count = _buffer.Count;
            if (count <= 0) return;
            float dt = _lastDeltaTime > 0f ? _lastDeltaTime : Time.deltaTime;
            for (int i = 0; i < count; i++)
            {
                var uv = _units[i];
                if (uv == null) continue;

                var pos = _buffer.Positions[i];
                uv.transform.position = new Vector3(pos.x, pos.y, 0f);
                uv.SetSpeed(_buffer.Speeds[i]);

                if (_buffer.Arrived[i] != 0)
                    uv.ClearDestinationSilent();

                var dir = _buffer.Directions[i];
                if (math.lengthsq(dir) > 0.0001f)
                    uv.ApplyFacing(new Vector3(dir.x, dir.y, 0f), dt);
            }
            _units.Clear();
        }

        private static void DisposeBuffer(ref Buffer buf)
        {
            if (buf.Positions.IsCreated) { buf.Positions.Dispose(); buf.Positions = default; }
            if (buf.Destinations.IsCreated) { buf.Destinations.Dispose(); buf.Destinations = default; }
            if (buf.HasDestination.IsCreated) { buf.HasDestination.Dispose(); buf.HasDestination = default; }
            if (buf.Speeds.IsCreated) { buf.Speeds.Dispose(); buf.Speeds = default; }
            if (buf.MaxSpeed.IsCreated) { buf.MaxSpeed.Dispose(); buf.MaxSpeed = default; }
            if (buf.Accel.IsCreated) { buf.Accel.Dispose(); buf.Accel = default; }
            if (buf.Decel.IsCreated) { buf.Decel.Dispose(); buf.Decel = default; }
            if (buf.Slowdown.IsCreated) { buf.Slowdown.Dispose(); buf.Slowdown = default; }
            if (buf.StopDist.IsCreated) { buf.StopDist.Dispose(); buf.StopDist = default; }
            if (buf.Steering.IsCreated) { buf.Steering.Dispose(); buf.Steering = default; }
            if (buf.OverrideVelocity.IsCreated) { buf.OverrideVelocity.Dispose(); buf.OverrideVelocity = default; }
            if (buf.HasOverride.IsCreated) { buf.HasOverride.Dispose(); buf.HasOverride = default; }
            if (buf.LastDirs.IsCreated) { buf.LastDirs.Dispose(); buf.LastDirs = default; }
            if (buf.Directions.IsCreated) { buf.Directions.Dispose(); buf.Directions = default; }
            if (buf.Arrived.IsCreated) { buf.Arrived.Dispose(); buf.Arrived = default; }
            buf.Capacity = 0;
            buf.Count = 0;
        }

        private struct MovementJob : IJobParallelFor
        {
            public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<float3> Destinations;
            [ReadOnly] public NativeArray<byte> HasDestination;
            public NativeArray<float> Speeds;
            [ReadOnly] public NativeArray<float> MaxSpeed;
            [ReadOnly] public NativeArray<float> Accel;
            [ReadOnly] public NativeArray<float> Decel;
            [ReadOnly] public NativeArray<float> Slowdown;
            [ReadOnly] public NativeArray<float> StopDist;
            [ReadOnly] public NativeArray<float3> Steering;
            [ReadOnly] public NativeArray<float3> OverrideVelocity;
            [ReadOnly] public NativeArray<byte> HasOverride;
            [ReadOnly] public NativeArray<float3> LastDirs;
            [WriteOnly] public NativeArray<float3> Directions;
            [WriteOnly] public NativeArray<byte> Arrived;
            [ReadOnly] public float DeltaTime;

            public void Execute(int index)
            {
                float3 pos = Positions[index];
                float speed = Speeds[index];
                if (HasDestination[index] == 0)
                {
                    float step = Decel[index] * DeltaTime;
                    speed = math.max(0f, speed - step);
                    Speeds[index] = speed;
                    Directions[index] = default;
                    Arrived[index] = 0;
                    return;
                }

                float3 dest = Destinations[index];
                float3 to = dest - pos;
                to.z = 0f;
                float dist = math.length(to);
                float stopDist = StopDist[index];
                if (dist <= stopDist)
                {
                    Positions[index] = dest;
                    Speeds[index] = 0f;
                    Directions[index] = dist > 0.0001f ? (to / dist) : default;
                    Arrived[index] = 1;
                    return;
                }

                float3 dirToDest = dist > 0.0001f ? (to / dist) : new float3(1f, 0f, 0f);
                bool useOverride = HasOverride[index] != 0;
                if (useOverride)
                {
                    float2 desiredVel = new float2(OverrideVelocity[index].x, OverrideVelocity[index].y);
                    float desiredSpeed = math.length(desiredVel);
                    float maxSpeed = MaxSpeed[index];
                    if (desiredSpeed > maxSpeed && desiredSpeed > 0.0001f)
                    {
                        desiredVel = desiredVel * (maxSpeed / desiredSpeed);
                        desiredSpeed = maxSpeed;
                    }

                    float2 lastDir = new float2(LastDirs[index].x, LastDirs[index].y);
                    if (math.lengthsq(lastDir) < 0.0001f)
                        lastDir = new float2(dirToDest.x, dirToDest.y);
                    float2 currentVel = lastDir * speed;
                    float2 deltaV = desiredVel - currentVel;
                    float deltaLen = math.length(deltaV);
                    float accel = desiredSpeed > math.length(currentVel) ? Accel[index] : Decel[index];
                    float maxDelta = accel * DeltaTime;
                    if (deltaLen > maxDelta && maxDelta > 0.0001f)
                        currentVel += (deltaV / deltaLen) * maxDelta;
                    else
                        currentVel = desiredVel;

                    speed = math.length(currentVel);
                    Speeds[index] = speed;
                    float3 dir = speed > 0.0001f ? new float3(currentVel.x / speed, currentVel.y / speed, 0f) : dirToDest;
                    float3 delta = new float3(currentVel.x, currentVel.y, 0f) * DeltaTime;
                    if (math.dot(delta, to) > 0f && math.lengthsq(delta) > dist * dist)
                        delta = to;
                    Positions[index] = pos + delta;
                    Directions[index] = dir;
                    Arrived[index] = 0;
                }
                else
                {
                    float desiredSpeed = MaxSpeed[index];
                    float slowdown = Slowdown[index];
                    if (slowdown > 0.0001f && dist < slowdown)
                    {
                        float t = math.saturate(dist / slowdown);
                        desiredSpeed = math.lerp(0.5f, desiredSpeed, t);
                    }

                    float accel = desiredSpeed > speed ? Accel[index] : Decel[index];
                    float stepSpeed = accel * DeltaTime;
                    if (speed < desiredSpeed)
                        speed = math.min(speed + stepSpeed, desiredSpeed);
                    else
                        speed = math.max(speed - stepSpeed, desiredSpeed);
                    Speeds[index] = speed;

                    float3 dir = dirToDest;
                    float3 steered = dir + Steering[index];
                    if (math.lengthsq(steered) > 0.0001f)
                        dir = math.normalize(steered);

                    float3 delta = dir * speed * DeltaTime;
                    if (math.lengthsq(delta) > dist * dist)
                        delta = to;
                    Positions[index] = pos + delta;
                    Directions[index] = dir;
                    Arrived[index] = 0;
                }
            }
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("MovementJobSystem");
            go.AddComponent<MovementJobSystem>();
        }
    }
}
