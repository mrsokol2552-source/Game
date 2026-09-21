/*
@file: My project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Jobs.cs
@module: presentation.performance.movement
@purpose: Extracted job struct and movement integration logic for MovementJobSystem.
@entry: MJOB-04
@api: MovementJobSystem partial job execution helpers
@deps: Unity Jobs, Unity.Mathematics, MovementJobSystem Native buffers
@data: per-unit movement state packed into NativeArrays
@perf: hotpath; every moving unit passes through this job during movement ticks
@thread: Unity job worker threads
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual movement verification
@config: BatchSize and movement settings packed by MovementJobSystem
@assets: none
@notes: future data-oriented migration can replace this job implementation without changing the caller contract
*/

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM-JOBS]
// Logical block: MovementJobSystem movement job extraction.

namespace Game.Presentation.Performance
{
    public partial class MovementJobSystem
    {
        // [MJOB-04]
        // Parallel movement integration and destination advance logic.
        [BurstCompile]
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
                var position = Positions[index];
                var speed = Speeds[index];
                if (HasDestination[index] == 0)
                {
                    var decelStep = Decel[index] * DeltaTime;
                    speed = math.max(0f, speed - decelStep);
                    Speeds[index] = speed;
                    Directions[index] = default;
                    Arrived[index] = 0;
                    return;
                }

                var destination = Destinations[index];
                var toTarget = destination - position;
                toTarget.z = 0f;
                var distance = math.length(toTarget);
                var stopDistance = StopDist[index];
                if (distance <= stopDistance)
                {
                    Positions[index] = destination;
                    Speeds[index] = 0f;
                    Directions[index] = distance > 0.0001f ? toTarget / distance : default;
                    Arrived[index] = 1;
                    return;
                }

                var directionToDestination = distance > 0.0001f ? toTarget / distance : new float3(1f, 0f, 0f);
                var useOverride = HasOverride[index] != 0;
                if (useOverride)
                {
                    var desiredVelocity = new float2(OverrideVelocity[index].x, OverrideVelocity[index].y);
                    var desiredSpeed = math.length(desiredVelocity);
                    var maxSpeed = MaxSpeed[index];
                    if (desiredSpeed > maxSpeed && desiredSpeed > 0.0001f)
                    {
                        desiredVelocity = desiredVelocity * (maxSpeed / desiredSpeed);
                        desiredSpeed = maxSpeed;
                    }

                    var lastDirection = new float2(LastDirs[index].x, LastDirs[index].y);
                    if (math.lengthsq(lastDirection) < 0.0001f)
                    {
                        lastDirection = new float2(directionToDestination.x, directionToDestination.y);
                    }

                    var currentVelocity = lastDirection * speed;
                    var deltaVelocity = desiredVelocity - currentVelocity;
                    var deltaLength = math.length(deltaVelocity);
                    var accel = desiredSpeed > math.length(currentVelocity) ? Accel[index] : Decel[index];
                    var maxDelta = accel * DeltaTime;
                    if (deltaLength > maxDelta && maxDelta > 0.0001f)
                    {
                        currentVelocity += (deltaVelocity / deltaLength) * maxDelta;
                    }
                    else
                    {
                        currentVelocity = desiredVelocity;
                    }

                    speed = math.length(currentVelocity);
                    Speeds[index] = speed;
                    var direction = speed > 0.0001f
                        ? new float3(currentVelocity.x / speed, currentVelocity.y / speed, 0f)
                        : directionToDestination;
                    var delta = new float3(currentVelocity.x, currentVelocity.y, 0f) * DeltaTime;
                    if (math.dot(delta, toTarget) > 0f && math.lengthsq(delta) > distance * distance)
                    {
                        delta = toTarget;
                    }

                    Positions[index] = position + delta;
                    Directions[index] = direction;
                    Arrived[index] = 0;
                    return;
                }

                var desiredTravelSpeed = MaxSpeed[index];
                var slowdownDistance = Slowdown[index];
                if (slowdownDistance > 0.0001f && distance < slowdownDistance)
                {
                    var t = math.saturate(distance / slowdownDistance);
                    desiredTravelSpeed = math.lerp(0.5f, desiredTravelSpeed, t);
                }

                var acceleration = desiredTravelSpeed > speed ? Accel[index] : Decel[index];
                var speedStep = acceleration * DeltaTime;
                if (speed < desiredTravelSpeed)
                {
                    speed = math.min(speed + speedStep, desiredTravelSpeed);
                }
                else
                {
                    speed = math.max(speed - speedStep, desiredTravelSpeed);
                }

                Speeds[index] = speed;
                var steeredDirection = directionToDestination + Steering[index];
                var finalDirection = math.lengthsq(steeredDirection) > 0.0001f
                    ? math.normalize(steeredDirection)
                    : directionToDestination;
                var travelDelta = finalDirection * speed * DeltaTime;
                if (math.lengthsq(travelDelta) > distance * distance)
                {
                    travelDelta = toTarget;
                }

                Positions[index] = position + travelDelta;
                Directions[index] = finalDirection;
                Arrived[index] = 0;
            }
        }
    }
}
