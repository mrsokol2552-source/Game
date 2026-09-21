/*
@file: My project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Buffers.cs
@module: presentation.movement.orca
@purpose: Extracted unit gathering, Native buffer management, snapshot ingestion, and velocity applyback for OrcaAvoidanceSystem.
@entry: ORCA-05
@api: OrcaAvoidanceSystem partial buffer helpers
@deps: UnitView, UnitCombat, UnitSoARegistry, NativeParallelMultiHashMap
@data: agent snapshots, cell hash buffers, avoidance outputs, NativeCollections
@perf: hotpath; allocation growth and fill cost scale with active ORCA agents and MaxNeighbors
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs
@config: CellSize, MinResponsibility, MaxNeighbors, UseSoARegistry
@assets: none
@notes: this layer is a strong seam for future SoA-driven avoidance extraction
*/

using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM-BUFFERS]
// Logical block: OrcaAvoidanceSystem buffer management extraction.

namespace Game.Presentation.Performance
{
    public partial class OrcaAvoidanceSystem
    {
        // [ORCA-05]
        // Unit gathering, Native buffer growth, snapshot ingestion, and applyback to UnitView.
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

                var movement = uv.GetMovementSettings();
                buf.MaxSpeed[i] = movement.MaxSpeed;

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
                    buf.Preferred[i] = dir * movement.MaxSpeed;
                }
                else
                {
                    buf.HasDestination[i] = 0;
                    buf.Preferred[i] = default;
                }

                buf.UseOrca[i] = uv.UseOrcaVelocity ? (byte)1 : (byte)0;
                float priority = Mathf.Clamp01(uv.OrcaPriority);
                float responsibility = Mathf.Max(0f, 1f - priority);
                float minResponsibility = Mathf.Max(0f, MinResponsibility);
                buf.Responsibility[i] = Mathf.Max(minResponsibility, responsibility);

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
                var velocity = buf.OutputVelocity[i];
                uv.SetVelocityOverride(new Vector3(velocity.x, velocity.y, 0f), applyFrame);
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
