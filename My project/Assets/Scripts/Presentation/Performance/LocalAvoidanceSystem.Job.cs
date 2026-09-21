/*
@file: My project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Job.cs
@module: presentation.performance
@purpose: Holds the legacy local-avoidance steering job and grid/hash helpers used to query spatial-hash neighborhoods.
@entry: LAVO-04
@api: partial job helpers for LocalAvoidanceSystem
@deps: Unity Jobs, NativeArray, NativeParallelMultiHashMap, Unity.Mathematics
@data: read-only snapshot arrays and write-only steering output
@perf: medium hotpath when ORCA is disabled and legacy avoidance is enabled
@thread: Unity job workers + static helper methods on main thread
@tests: covered indirectly by repo audits and runtime movement regressions
@config: avoid radius, rings, max neighbors, and strength affect solver behavior
@notes: this is intentionally simpler than ORCA and should remain isolated as a fallback path
*/

using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-LOCALAVOIDANCESYSTEM-JOB]
// Logical block: Scripts/Presentation/Performance/LocalAvoidanceSystem.Job.

namespace Game.Presentation.Performance
{
    public partial class LocalAvoidanceSystem
    {
        // [LAVO-04]
        // Burst-friendly steering job plus cell/hash helpers for the legacy local-avoidance spatial hash.
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
                int faction = Factions[index];
                var cell = Cells[index];
                bool stop = false;

                for (int dy = -Rings; dy <= Rings && !stop; dy++)
                {
                    for (int dx = -Rings; dx <= Rings && !stop; dx++)
                    {
                        int key = HashKey(cell.x + dx, cell.y + dy);
                        if (!Buckets.TryGetFirstValue(key, out var otherIdx, out var iterator))
                            continue;

                        do
                        {
                            if (otherIdx == index) continue;
                            if (!AvoidEnemies && Factions[otherIdx] != faction) continue;

                            float3 delta = pos - Positions[otherIdx];
                            float dist2 = math.lengthsq(delta);
                            if (dist2 <= 0.0001f || dist2 > RadiusSq) continue;

                            float weight = 1f - (dist2 * InvRadiusSq);
                            if (weight <= 0f) continue;

                            float invDistance = math.rsqrt(dist2);
                            sum += delta * invDistance * weight;

                            if (MaxNeighbors > 0 && ++neighbors >= MaxNeighbors)
                            {
                                stop = true;
                                break;
                            }
                        }
                        while (Buckets.TryGetNextValue(out otherIdx, ref iterator));
                    }
                }

                float magnitude = math.length(sum);
                if (magnitude > 0.0001f)
                {
                    float3 direction = sum / magnitude;
                    float scaled = math.min(MaxSteer, magnitude * Strength);
                    Steering[index] = direction * scaled;
                }
                else
                {
                    Steering[index] = default;
                }
            }
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
                int hash = 73856093 ^ x;
                hash = (hash * 19349663) ^ y;
                return hash;
            }
        }
    }
}
