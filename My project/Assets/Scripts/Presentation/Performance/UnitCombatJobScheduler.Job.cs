/*
@file: My project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Job.cs
@module: presentation.combat.jobs
@purpose: Extracted nearest-enemy job implementation for UnitCombatJobScheduler.
@entry: UCJS-04
@api: UnitCombatJobScheduler partial job execution helpers
@deps: Unity Jobs, NativeParallelMultiHashMap
@data: positions, factions, hash cells, nearest-enemy output indices
@perf: hotpath; every non-squad combat unit passes through this job when scheduled
@thread: Unity job worker threads
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs
@config: HashRings
@assets: none
@notes: this is a clean seam for future compute/data-oriented nearest-target experiments
*/

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-UNITCOMBATJOBSCHEDULER-JOB]
// Logical block: UnitCombatJobScheduler nearest-enemy job extraction.

namespace Game.Presentation.Performance
{
    public partial class UnitCombatJobScheduler
    {
        // [UCJS-04]
        // Parallel nearest-enemy search inside spatial-hash neighborhoods.
        [BurstCompile]
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
                var position = Positions[index];
                var faction = Factions[index];
                var bestDistance = float.MaxValue;
                var bestIndex = -1;
                var myCell = Cells[index];
                var rings = Mathf.Max(0, Rings);

                for (int dy = -rings; dy <= rings; dy++)
                {
                    for (int dx = -rings; dx <= rings; dx++)
                    {
                        var key = HashKey(myCell.x + dx, myCell.y + dy);
                        if (!Buckets.TryGetFirstValue(key, out var otherIndex, out var iterator))
                        {
                            continue;
                        }

                        do
                        {
                            if (otherIndex == index || Factions[otherIndex] == faction)
                            {
                                continue;
                            }

                            var distanceSquared = (Positions[otherIndex] - position).sqrMagnitude;
                            if (distanceSquared >= bestDistance)
                            {
                                continue;
                            }

                            bestDistance = distanceSquared;
                            bestIndex = otherIndex;
                        }
                        while (Buckets.TryGetNextValue(out otherIndex, ref iterator));
                    }
                }

                Nearest[index] = bestIndex;
            }
        }
    }
}
