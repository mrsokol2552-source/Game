/*
@file: My project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Buffers.cs
@module: presentation.performance.movement
@purpose: Extracted unit gathering, buffer allocation, and result application for MovementJobSystem.
@entry: MJOB-05
@api: MovementJobSystem partial buffer management helpers
@deps: UnitView runtime state and NativeArray batch buffers
@data: gathered UnitView list, movement buffer arrays, per-tick applyback state
@perf: hotpath; allocation growth and applyback cost scale directly with visible moving units
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual movement verification
@config: Enabled, Interval, BatchSize
@assets: none
@notes: this layer should stay allocation-stable; future SoA/data extraction can target it cleanly
*/

using Game.Presentation.View;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM-BUFFERS]
// Logical block: MovementJobSystem unit gathering, Native buffers, and applyback extraction.

namespace Game.Presentation.Performance
{
    public partial class MovementJobSystem
    {
        // [MJOB-05]
        // Unit gathering, NativeArray buffer growth, input fill, and result applyback.
        private int GatherUnits()
        {
            _units.Clear();
            foreach (var unit in UnitView.All)
            {
                if (unit == null || !unit.isActiveAndEnabled || !unit.UseMovementJobs)
                {
                    continue;
                }

                _units.Add(unit);
            }

            return _units.Count;
        }

        private void EnsureCapacity(ref Buffer buf, int count)
        {
            if (count <= buf.Capacity)
            {
                return;
            }

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
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                var unit = _units[i];
                var position = unit.transform.position;
                buf.Positions[i] = position;
                buf.Speeds[i] = unit.GetSpeed();

                var movement = unit.GetMovementSettings();
                buf.MaxSpeed[i] = movement.MaxSpeed;
                buf.Accel[i] = movement.Acceleration;
                buf.Decel[i] = movement.Deceleration;
                buf.Slowdown[i] = movement.SlowdownDistance;
                buf.StopDist[i] = movement.StopDistance;

                if (unit.TryGetDestination(out var destination))
                {
                    buf.HasDestination[i] = 1;
                    buf.Destinations[i] = destination;
                }
                else
                {
                    buf.HasDestination[i] = 0;
                    buf.Destinations[i] = position;
                }

                if (unit.TryGetSteering(out var steering))
                {
                    buf.Steering[i] = steering * unit.SteeringInfluence;
                }
                else
                {
                    buf.Steering[i] = default;
                }

                if (unit.TryGetVelocityOverride(out var velocityOverride))
                {
                    buf.HasOverride[i] = 1;
                    buf.OverrideVelocity[i] = velocityOverride;
                }
                else
                {
                    buf.HasOverride[i] = 0;
                    buf.OverrideVelocity[i] = default;
                }

                buf.LastDirs[i] = unit.GetLastDirection();
                buf.Directions[i] = default;
                buf.Arrived[i] = 0;
            }

            return true;
        }

        private void ApplyResults()
        {
            var count = _buffer.Count;
            if (count <= 0)
            {
                return;
            }

            var deltaTime = _lastDeltaTime > 0f ? _lastDeltaTime : Time.deltaTime;
            for (int i = 0; i < count; i++)
            {
                var unit = _units[i];
                if (unit == null)
                {
                    continue;
                }

                var position = _buffer.Positions[i];
                unit.transform.position = new Vector3(position.x, position.y, 0f);
                unit.SetSpeed(_buffer.Speeds[i]);

                if (_buffer.Arrived[i] != 0)
                {
                    unit.ClearDestinationSilent();
                }

                var direction = _buffer.Directions[i];
                if (math.lengthsq(direction) > 0.0001f)
                {
                    unit.ApplyFacing(new Vector3(direction.x, direction.y, 0f), deltaTime);
                }
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
    }
}
