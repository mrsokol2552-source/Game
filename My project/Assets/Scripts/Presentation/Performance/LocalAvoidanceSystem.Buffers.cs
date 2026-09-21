/*
@file: My project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Buffers.cs
@module: presentation.performance
@purpose: Maintains legacy local-avoidance unit snapshots, Native buffer growth, and applyback into UnitView steering.
@entry: LAVO-03
@api: partial buffer helpers for LocalAvoidanceSystem
@deps: UnitCombat, UnitView, NativeArray, NativeParallelMultiHashMap
@data: double-buffered positions/factions/destination flags/spatial-hash buckets
@perf: medium hotpath when legacy avoidance is active
@thread: main thread only
@tests: covered indirectly by repo audits and runtime movement regressions
@config: CellSize and snapshot size drive buffer layout
@notes: extracted to keep LocalAvoidanceSystem scheduling code readable before larger movement/rendering migration
*/

using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-LOCALAVOIDANCESYSTEM-BUFFERS]
// Logical block: Scripts/Presentation/Performance/LocalAvoidanceSystem.Buffers.

namespace Game.Presentation.Performance
{
    public partial class LocalAvoidanceSystem
    {
        // [LAVO-03]
        // Unit gathering, Native buffer growth/fill, and steering applyback for legacy local avoidance.
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
    }
}
