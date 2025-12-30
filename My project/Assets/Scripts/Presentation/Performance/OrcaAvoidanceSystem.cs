using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Presentation.Performance
{
    /// <summary>
    /// ORCA/RVO local avoidance system using a spatial hash + job.
    /// Produces per-unit velocity overrides for the next frame.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class OrcaAvoidanceSystem : MonoBehaviour
    {
        public static OrcaAvoidanceSystem Instance { get; private set; }
        public static bool IsActive => Instance != null && Instance.Enabled;

        [Tooltip("Enable ORCA/RVO avoidance.")]
        public bool Enabled = true;
        [Tooltip("How often to recompute ORCA (seconds). 0 = every frame.")]
        public float Interval = 0f;
        [Tooltip("If true, updates are driven externally.")]
        public bool ExternalUpdate = false;
        [Tooltip("World cell size for spatial hash.")]
        public float CellSize = 1.5f;
        [Tooltip("Neighbor search radius in world units.")]
        public float NeighborDist = 2.5f;
        [Tooltip("Max neighbors considered per unit (higher = more stable but slower).")]
        public int MaxNeighbors = 12;
        [Tooltip("Agent radius in world units.")]
        public float AgentRadius = 0.35f;
        [Tooltip("Time horizon (seconds) for collision avoidance.")]
        public float TimeHorizon = 1.5f;
        [Header("Cohesion")]
        [Tooltip("Bias preferred velocity toward local friendly centroid.")]
        public bool UseCohesion = true;
        [Tooltip("Radius to sample friendly centroid (world units).")]
        public float CohesionRadius = 4f;
        [Tooltip("Weight of cohesion contribution (0..1).")]
        public float CohesionWeight = 0.3f;
        [Tooltip("Max cohesion speed as a fraction of max speed.")]
        public float CohesionMaxSpeedFraction = 0.5f;
        [Tooltip("Avoid enemies as well as allies.")]
        public bool AvoidEnemies = true;
        [Tooltip("Skip units without destination (do not override).")]
        public bool SkipWithoutDestination = true;
        [Tooltip("Job batch size.")]
        public int BatchSize = 32;
        [Tooltip("Minimum responsibility weight for avoidance (prevents zero-weight agents).")]
        public float MinResponsibility = 0.05f;
        [Tooltip("Use UnitSoARegistry for input data when available.")]
        public bool UseSoARegistry = true;

        private readonly List<UnitView>[] _unitBuffers =
        {
            new List<UnitView>(256),
            new List<UnitView>(256)
        };

        private struct Line
        {
            public float2 point;
            public float2 direction;
        }

        private struct Buffer
        {
            public NativeArray<float2> Positions;
            public NativeArray<float2> Velocities;
            public NativeArray<float2> Preferred;
            public NativeArray<float> MaxSpeed;
            public NativeArray<byte> HasDestination;
            public NativeArray<byte> UseOrca;
            public NativeArray<float> Responsibility;
            public NativeArray<int> Factions;
            public NativeArray<float2> OutputVelocity;
            public NativeArray<int2> Cells;
            public NativeParallelMultiHashMap<int, int> Buckets;
            [NativeDisableParallelForRestriction] public NativeArray<Line> Lines;
            [NativeDisableParallelForRestriction] public NativeArray<Line> ScratchLines;
            public int Capacity;
            public int BucketCapacity;
            public int LineCapacity;
            public int MaxNeighbors;
            public int Count;
        }

        private readonly Buffer[] _buffers = new Buffer[2];
        private int _activeBuffer = -1;
        private JobHandle _jobHandle;
        private bool _jobActive;
        private float _timer;

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
            if (ExternalUpdate) return;
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (_jobActive && _jobHandle.IsCompleted)
            {
                _jobHandle.Complete();
                ApplyResults(_activeBuffer);
                _jobActive = false;
            }

            if (!Enabled) return;
            if (_jobActive) return;

            if (Interval > 0f)
            {
                _timer -= deltaTime;
                if (_timer > 0f) return;
                _timer = Interval;
            }

            int nextBuffer = _activeBuffer == 0 ? 1 : 0;
            var units = _unitBuffers[nextBuffer];
            int count;
            UnitSoARegistry.OrcaSnapshot snapshot = default;
            bool usingSnapshot = false;
            var registry = UnitSoARegistry.Instance;
            if (UseSoARegistry && registry != null && registry.TryGetOrcaSnapshot(out snapshot))
            {
                units.Clear();
                if (units.Capacity < snapshot.Count) units.Capacity = snapshot.Count;
                units.AddRange(snapshot.Units);
                count = snapshot.Count;
                usingSnapshot = count > 0;
            }
            else
            {
                count = GatherUnits(units);
            }
            if (count <= 0) return;

            int effectiveMaxNeighbors = Mathf.Max(1, MaxNeighbors);
            ref var buf = ref _buffers[nextBuffer];
            EnsureCapacity(ref buf, count, effectiveMaxNeighbors);
            if (usingSnapshot)
            {
                if (!FillArraysFromSnapshot(ref buf, snapshot, count))
                    return;
            }
            else
            {
                if (!FillArrays(ref buf, units, count))
                    return;
            }

            int rings = Mathf.Max(1, Mathf.CeilToInt(NeighborDist / Mathf.Max(0.0001f, CellSize)));
            var job = new OrcaJob
            {
                Positions = buf.Positions,
                Velocities = buf.Velocities,
                Preferred = buf.Preferred,
                MaxSpeed = buf.MaxSpeed,
                HasDestination = buf.HasDestination,
                UseOrca = buf.UseOrca,
                Responsibility = buf.Responsibility,
                Factions = buf.Factions,
                OutputVelocity = buf.OutputVelocity,
                Cells = buf.Cells,
                Buckets = buf.Buckets.AsReadOnly(),
                Lines = buf.Lines,
                ScratchLines = buf.ScratchLines,
                MaxNeighbors = effectiveMaxNeighbors,
                NeighborDistSq = NeighborDist * NeighborDist,
                AgentRadius = Mathf.Max(0.01f, AgentRadius),
                TimeHorizon = Mathf.Max(0.05f, TimeHorizon),
                DeltaTime = deltaTime,
                Rings = rings,
                UseCohesion = UseCohesion && CohesionWeight > 0f && CohesionRadius > 0f,
                CohesionRadiusSq = CohesionRadius * CohesionRadius,
                CohesionWeight = Mathf.Max(0f, CohesionWeight),
                CohesionMaxSpeedFraction = Mathf.Max(0f, CohesionMaxSpeedFraction),
                AvoidEnemies = AvoidEnemies,
                SkipWithoutDestination = SkipWithoutDestination
            };

            _jobHandle = job.Schedule(count, Mathf.Max(1, BatchSize));
            _jobActive = true;
            _activeBuffer = nextBuffer;
            buf.Count = count;
        }

        private int GatherUnits(List<UnitView> target)
        {
            target.Clear();
            foreach (var uv in UnitView.All)
            {
                if (uv == null || !uv.isActiveAndEnabled) continue;
                target.Add(uv);
            }
            return target.Count;
        }

        private void EnsureCapacity(ref Buffer buf, int count, int maxNeighbors)
        {
            bool needsResize = count > buf.Capacity || buf.MaxNeighbors != maxNeighbors;
            if (!needsResize && buf.Buckets.IsCreated && buf.BucketCapacity >= count * 2 && buf.LineCapacity >= count * maxNeighbors)
                return;

            DisposeBuffer(ref buf);
            buf.Capacity = Mathf.NextPowerOfTwo(Mathf.Max(4, count));
            buf.MaxNeighbors = maxNeighbors;
            buf.Positions = new NativeArray<float2>(buf.Capacity, Allocator.Persistent);
            buf.Velocities = new NativeArray<float2>(buf.Capacity, Allocator.Persistent);
            buf.Preferred = new NativeArray<float2>(buf.Capacity, Allocator.Persistent);
            buf.MaxSpeed = new NativeArray<float>(buf.Capacity, Allocator.Persistent);
            buf.HasDestination = new NativeArray<byte>(buf.Capacity, Allocator.Persistent);
            buf.UseOrca = new NativeArray<byte>(buf.Capacity, Allocator.Persistent);
            buf.Responsibility = new NativeArray<float>(buf.Capacity, Allocator.Persistent);
            buf.Factions = new NativeArray<int>(buf.Capacity, Allocator.Persistent);
            buf.OutputVelocity = new NativeArray<float2>(buf.Capacity, Allocator.Persistent);
            buf.Cells = new NativeArray<int2>(buf.Capacity, Allocator.Persistent);
            buf.BucketCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, count * 2));
            buf.Buckets = new NativeParallelMultiHashMap<int, int>(buf.BucketCapacity, Allocator.Persistent);
            buf.LineCapacity = buf.Capacity * maxNeighbors;
            buf.Lines = new NativeArray<Line>(buf.LineCapacity, Allocator.Persistent);
            buf.ScratchLines = new NativeArray<Line>(buf.LineCapacity, Allocator.Persistent);
        }

        private bool FillArrays(ref Buffer buf, List<UnitView> units, int count)
        {
            if (!buf.Positions.IsCreated || !buf.Velocities.IsCreated || !buf.Preferred.IsCreated ||
                !buf.MaxSpeed.IsCreated || !buf.HasDestination.IsCreated || !buf.UseOrca.IsCreated || !buf.Responsibility.IsCreated || !buf.Factions.IsCreated ||
                !buf.OutputVelocity.IsCreated || !buf.Cells.IsCreated || !buf.Buckets.IsCreated ||
                !buf.Lines.IsCreated || !buf.ScratchLines.IsCreated)
                return false;

            buf.Buckets.Clear();
            for (int i = 0; i < count; i++)
            {
                var uv = units[i];
                var pos3 = uv.transform.position;
                var pos = new float2(pos3.x, pos3.y);
                buf.Positions[i] = pos;

                var m = uv.GetMovementSettings();
                buf.MaxSpeed[i] = m.MaxSpeed;

                Vector3 lastDir3 = uv.GetLastDirection();
                float2 lastDir = new float2(lastDir3.x, lastDir3.y);
                float speed = uv.GetSpeed();
                buf.Velocities[i] = lastDir * speed;

                if (uv.TryGetDestination(out var dest))
                {
                    buf.HasDestination[i] = 1;
                    float2 to = new float2(dest.x - pos3.x, dest.y - pos3.y);
                    float len = math.length(to);
                    float2 dir = len > 0.0001f ? (to / len) : new float2(1f, 0f);
                    buf.Preferred[i] = dir * m.MaxSpeed;
                }
                else
                {
                    buf.HasDestination[i] = 0;
                    buf.Preferred[i] = default;
                }

                buf.UseOrca[i] = uv.UseOrcaVelocity ? (byte)1 : (byte)0;
                float priority = Mathf.Clamp01(uv.OrcaPriority);
                float resp = Mathf.Max(0f, 1f - priority);
                float minResp = Mathf.Max(0f, MinResponsibility);
                buf.Responsibility[i] = Mathf.Max(minResp, resp);

                var combat = uv.GetComponent<UnitCombat>();
                buf.Factions[i] = combat != null ? (int)combat.Faction : 0;

                var cell = ToCell(pos3, CellSize);
                buf.Cells[i] = cell;
                buf.Buckets.Add(HashKey(cell.x, cell.y), i);
            }
            return true;
        }

        private bool FillArraysFromSnapshot(ref Buffer buf, UnitSoARegistry.OrcaSnapshot snap, int count)
        {
            if (!buf.Positions.IsCreated || !buf.Velocities.IsCreated || !buf.Preferred.IsCreated ||
                !buf.MaxSpeed.IsCreated || !buf.HasDestination.IsCreated || !buf.UseOrca.IsCreated || !buf.Responsibility.IsCreated || !buf.Factions.IsCreated ||
                !buf.OutputVelocity.IsCreated || !buf.Cells.IsCreated || !buf.Buckets.IsCreated ||
                !buf.Lines.IsCreated || !buf.ScratchLines.IsCreated)
                return false;

            if (!snap.Positions.IsCreated || !snap.Velocities.IsCreated || !snap.Preferred.IsCreated ||
                !snap.MaxSpeed.IsCreated || !snap.HasDestination.IsCreated || !snap.UseOrca.IsCreated || !snap.Responsibility.IsCreated || !snap.Factions.IsCreated ||
                !snap.Cells.IsCreated)
                return false;

            NativeArray<float2>.Copy(snap.Positions, buf.Positions, count);
            NativeArray<float2>.Copy(snap.Velocities, buf.Velocities, count);
            NativeArray<float2>.Copy(snap.Preferred, buf.Preferred, count);
            NativeArray<float>.Copy(snap.MaxSpeed, buf.MaxSpeed, count);
            NativeArray<byte>.Copy(snap.HasDestination, buf.HasDestination, count);
            NativeArray<byte>.Copy(snap.UseOrca, buf.UseOrca, count);
            NativeArray<float>.Copy(snap.Responsibility, buf.Responsibility, count);
            NativeArray<int>.Copy(snap.Factions, buf.Factions, count);
            NativeArray<int2>.Copy(snap.Cells, buf.Cells, count);

            buf.Buckets.Clear();
            for (int i = 0; i < count; i++)
            {
                var cell = buf.Cells[i];
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
                if (buf.HasDestination[i] == 0 || buf.UseOrca[i] == 0) continue;
                var uv = units[i];
                if (uv == null) continue;
                var vel = buf.OutputVelocity[i];
                uv.SetVelocityOverride(new Vector3(vel.x, vel.y, 0f), applyFrame);
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
            if (buf.Velocities.IsCreated) { buf.Velocities.Dispose(); buf.Velocities = default; }
            if (buf.Preferred.IsCreated) { buf.Preferred.Dispose(); buf.Preferred = default; }
            if (buf.MaxSpeed.IsCreated) { buf.MaxSpeed.Dispose(); buf.MaxSpeed = default; }
            if (buf.HasDestination.IsCreated) { buf.HasDestination.Dispose(); buf.HasDestination = default; }
            if (buf.UseOrca.IsCreated) { buf.UseOrca.Dispose(); buf.UseOrca = default; }
            if (buf.Responsibility.IsCreated) { buf.Responsibility.Dispose(); buf.Responsibility = default; }
            if (buf.Factions.IsCreated) { buf.Factions.Dispose(); buf.Factions = default; }
            if (buf.OutputVelocity.IsCreated) { buf.OutputVelocity.Dispose(); buf.OutputVelocity = default; }
            if (buf.Cells.IsCreated) { buf.Cells.Dispose(); buf.Cells = default; }
            if (buf.Buckets.IsCreated) { buf.Buckets.Dispose(); buf.Buckets = default; }
            if (buf.Lines.IsCreated) { buf.Lines.Dispose(); buf.Lines = default; }
            if (buf.ScratchLines.IsCreated) { buf.ScratchLines.Dispose(); buf.ScratchLines = default; }
            buf.Capacity = 0;
            buf.BucketCapacity = 0;
            buf.LineCapacity = 0;
            buf.MaxNeighbors = 0;
            buf.Count = 0;
        }

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

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("OrcaAvoidanceSystem");
            go.AddComponent<OrcaAvoidanceSystem>();
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
