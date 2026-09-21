/*
@file: My project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Job.cs
@module: presentation.movement.orca
@purpose: Extracted ORCA solver job and linear-program helpers for OrcaAvoidanceSystem.
@entry: ORCA-04
@api: OrcaAvoidanceSystem partial ORCA job solver
@deps: Unity Burst, Unity Jobs, NativeParallelMultiHashMap
@data: positions, velocities, preferred vectors, responsibility weights, hash cells, and ORCA line buffers
@perf: hotpath; O(neighbors) per active agent with LP refinement and cohesion blending
@thread: Unity job worker threads
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs
@config: NeighborDist, AgentRadius, TimeHorizon, BatchSize, UseCohesion
@assets: none
@notes: this seam should survive a later transition to a more data-oriented or compute-driven avoidance backend
*/

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM-JOB]
// Logical block: OrcaAvoidanceSystem ORCA solver job extraction.

namespace Game.Presentation.Performance
{
    public partial class OrcaAvoidanceSystem
    {
        // [ORCA-04]
        // Parallel ORCA solver that computes velocity constraints and final avoidance vectors.
        [BurstCompile]
        private struct OrcaJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float2> Positions;
            [ReadOnly] public NativeArray<float2> Velocities;
            [ReadOnly] public NativeArray<float2> Preferred;
            [ReadOnly] public NativeArray<float> MaxSpeed;
            [ReadOnly] public NativeArray<byte> HasDestination;
            [ReadOnly] public NativeArray<byte> UseOrca;
            [ReadOnly] public NativeArray<float> Responsibility;
            [ReadOnly] public NativeArray<int> Factions;
            [WriteOnly] public NativeArray<float2> OutputVelocity;
            [ReadOnly] public NativeArray<int2> Cells;
            [ReadOnly] public NativeParallelMultiHashMap<int, int>.ReadOnly Buckets;
            [NativeDisableParallelForRestriction] public NativeArray<Line> Lines;
            [NativeDisableParallelForRestriction] public NativeArray<Line> ScratchLines;
            [ReadOnly] public int MaxNeighbors;
            [ReadOnly] public float NeighborDistSq;
            [ReadOnly] public float AgentRadius;
            [ReadOnly] public float TimeHorizon;
            [ReadOnly] public float DeltaTime;
            [ReadOnly] public int Rings;
            [ReadOnly] public bool UseCohesion;
            [ReadOnly] public float CohesionRadiusSq;
            [ReadOnly] public float CohesionWeight;
            [ReadOnly] public float CohesionMaxSpeedFraction;
            [ReadOnly] public bool AvoidEnemies;
            [ReadOnly] public bool SkipWithoutDestination;

            public void Execute(int index)
            {
                if (UseOrca[index] == 0)
                {
                    OutputVelocity[index] = default;
                    return;
                }
                if (SkipWithoutDestination && HasDestination[index] == 0)
                {
                    OutputVelocity[index] = default;
                    return;
                }

                float2 position = Positions[index];
                float2 velocity = Velocities[index];
                float2 prefVelocity = Preferred[index];
                float maxSpeed = MaxSpeed[index];
                int faction = Factions[index];
                float respSelf = Responsibility[index];

                int lineBase = index * MaxNeighbors;
                int lineCount = 0;
                float2 cohesionSum = default;
                int cohesionCount = 0;
                float combinedRadius = AgentRadius * 2f;
                float combinedRadiusSq = combinedRadius * combinedRadius;
                float invTimeHorizon = 1f / math.max(0.0001f, TimeHorizon);
                float invTimeStep = 1f / math.max(0.0001f, DeltaTime);

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
                            if (!AvoidEnemies && Factions[otherIdx] != faction) continue;

                            float2 otherPos = Positions[otherIdx];
                            float2 relPos = otherPos - position;
                            float distSq = math.lengthsq(relPos);
                            if (distSq > NeighborDistSq) continue;

                            if (UseCohesion && Factions[otherIdx] == faction && distSq <= CohesionRadiusSq)
                            {
                                cohesionSum += otherPos;
                                cohesionCount++;
                            }

                            float2 otherVel = Velocities[otherIdx];
                            float2 relVel = velocity - otherVel;
                            Line line;
                            float2 u;
                            float respOther = Responsibility[otherIdx];
                            float denom = math.max(0.0001f, respSelf + respOther);
                            float weight = respSelf / denom;

                            if (distSq > combinedRadiusSq)
                            {
                                float2 w = relVel - invTimeHorizon * relPos;
                                float wLengthSq = math.lengthsq(w);
                                float dotProduct1 = math.dot(w, relPos);
                                if (dotProduct1 < 0f && dotProduct1 * dotProduct1 > combinedRadiusSq * wLengthSq)
                                {
                                    float wLength = math.sqrt(wLengthSq);
                                    float2 unitW = wLength > 0.0001f ? (w / wLength) : new float2(1f, 0f);
                                    line.direction = new float2(unitW.y, -unitW.x);
                                    u = (combinedRadius * invTimeHorizon - wLength) * unitW;
                                }
                                else
                                {
                                    float leg = math.sqrt(math.max(0f, distSq - combinedRadiusSq));
                                    float det = Det(relPos, w);
                                    if (det > 0f)
                                    {
                                        line.direction = (relPos * leg - Perp(relPos) * combinedRadius) / distSq;
                                    }
                                    else
                                    {
                                        line.direction = (-relPos * leg - Perp(relPos) * combinedRadius) / distSq;
                                    }
                                    float dotProduct2 = math.dot(relVel, line.direction);
                                    u = dotProduct2 * line.direction - relVel;
                                }
                            }
                            else
                            {
                                float2 w = relVel - invTimeStep * relPos;
                                float wLength = math.length(w);
                                float2 unitW = wLength > 0.0001f ? (w / wLength) : new float2(1f, 0f);
                                line.direction = new float2(unitW.y, -unitW.x);
                                u = (combinedRadius * invTimeStep - wLength) * unitW;
                            }

                            line.point = velocity + weight * u;
                            if (lineCount < MaxNeighbors)
                            {
                                Lines[lineBase + lineCount] = line;
                                lineCount++;
                            }
                            if (MaxNeighbors > 0 && lineCount >= MaxNeighbors)
                            {
                                stop = true;
                                break;
                            }
                        }
                        while (Buckets.TryGetNextValue(out otherIdx, ref it));
                    }
                }

                float2 desired = prefVelocity;
                if (UseCohesion && cohesionCount > 0)
                {
                    float2 center = cohesionSum / math.max(1, cohesionCount);
                    float2 toCenter = center - position;
                    float len = math.length(toCenter);
                    if (len > 0.0001f)
                    {
                        float2 dir = toCenter / len;
                        float maxCohesionSpeed = maxSpeed * math.saturate(CohesionMaxSpeedFraction);
                        float cohesionScale = CohesionRadiusSq > 0.0001f ? math.saturate(len / math.sqrt(CohesionRadiusSq)) : 1f;
                        float2 cohesionVel = dir * (maxCohesionSpeed * CohesionWeight * cohesionScale);
                        desired += cohesionVel;
                    }
                }

                float2 result;
                int lineFail = LinearProgram2(Lines, lineBase, lineCount, maxSpeed, desired, false, out result);
                if (lineFail < lineCount)
                {
                    LinearProgram3(Lines, ScratchLines, lineBase, lineCount, lineFail, maxSpeed, ref result);
                }
                OutputVelocity[index] = result;
            }

            private static int LinearProgram2(NativeArray<Line> lines, int start, int count, float radius, float2 optVelocity, bool directionOpt, out float2 result)
            {
                if (directionOpt)
                {
                    result = optVelocity * radius;
                }
                else if (math.lengthsq(optVelocity) > radius * radius)
                {
                    result = math.normalize(optVelocity) * radius;
                }
                else
                {
                    result = optVelocity;
                }

                for (int i = 0; i < count; i++)
                {
                    Line line = lines[start + i];
                    if (Det(line.direction, line.point - result) > 0f)
                    {
                        float2 temp = result;
                        if (!LinearProgram1(lines, start, i, count, radius, optVelocity, directionOpt, out result))
                        {
                            result = temp;
                            return i;
                        }
                    }
                }
                return count;
            }

            private static bool LinearProgram1(NativeArray<Line> lines, int start, int lineNo, int count, float radius, float2 optVelocity, bool directionOpt, out float2 result)
            {
                Line line = lines[start + lineNo];
                float dot = math.dot(line.point, line.direction);
                float discriminant = dot * dot + radius * radius - math.dot(line.point, line.point);

                if (discriminant < 0f)
                {
                    result = default;
                    return false;
                }

                float sqrtDisc = math.sqrt(discriminant);
                float tLeft = -dot - sqrtDisc;
                float tRight = -dot + sqrtDisc;

                for (int i = 0; i < lineNo; i++)
                {
                    Line lineI = lines[start + i];
                    float determinant = Det(line.direction, lineI.direction);
                    float numerator = Det(lineI.direction, line.point - lineI.point);

                    if (math.abs(determinant) <= 1e-6f)
                    {
                        if (numerator < 0f)
                        {
                            result = default;
                            return false;
                        }
                        continue;
                    }

                    float t = numerator / determinant;
                    if (determinant >= 0f)
                        tRight = math.min(tRight, t);
                    else
                        tLeft = math.max(tLeft, t);

                    if (tLeft > tRight)
                    {
                        result = default;
                        return false;
                    }
                }

                if (directionOpt)
                {
                    if (math.dot(optVelocity, line.direction) > 0f)
                        result = line.point + tRight * line.direction;
                    else
                        result = line.point + tLeft * line.direction;
                }
                else
                {
                    float t = math.dot(line.direction, optVelocity - line.point);
                    if (t < tLeft)
                        t = tLeft;
                    else if (t > tRight)
                        t = tRight;
                    result = line.point + t * line.direction;
                }

                return true;
            }

            private static void LinearProgram3(NativeArray<Line> lines, NativeArray<Line> scratch, int start, int count, int beginLine, float radius, ref float2 result)
            {
                float distance = 0f;
                for (int i = beginLine; i < count; i++)
                {
                    Line line = lines[start + i];
                    if (Det(line.direction, line.point - result) > distance)
                    {
                        int scratchCount = 0;
                        for (int j = 0; j < i; j++)
                        {
                            Line lineJ = lines[start + j];
                            float determinant = Det(line.direction, lineJ.direction);
                            Line proj;
                            if (math.abs(determinant) <= 1e-6f)
                            {
                                if (Det(line.direction, lineJ.point - line.point) > 0f)
                                    continue;
                                proj.point = (line.point + lineJ.point) * 0.5f;
                            }
                            else
                            {
                                proj.point = line.point + (Det(lineJ.direction, line.point - lineJ.point) / determinant) * line.direction;
                            }
                            float2 dir = lineJ.direction - line.direction * math.dot(lineJ.direction, line.direction);
                            float dirLen = math.length(dir);
                            if (dirLen > 0.0001f)
                                dir /= dirLen;
                            else
                                dir = new float2(-line.direction.y, line.direction.x);
                            proj.direction = dir;
                            scratch[start + scratchCount] = proj;
                            scratchCount++;
                        }

                        float2 temp = result;
                        int lp2 = LinearProgram2(scratch, start, scratchCount, radius, new float2(-line.direction.y, line.direction.x), true, out result);
                        if (lp2 < scratchCount)
                            result = temp;
                        distance = Det(line.direction, line.point - result);
                    }
                }
            }

            private static float2 Perp(float2 v) => new float2(v.y, -v.x);

            private static float Det(float2 a, float2 b) => a.x * b.y - a.y * b.x;

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
}
