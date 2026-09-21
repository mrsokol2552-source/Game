/*
@file: My project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Buffers.cs
@module: presentation.combat.jobs
@purpose: Extracted Native buffer management, snapshot fill, and applyback for UnitCombatJobScheduler.
@entry: UCJS-03
@api: UnitCombatJobScheduler partial buffer helpers
@deps: UnitCombat, UnitSoARegistry, NativeParallelMultiHashMap
@data: per-buffer unit snapshots, hash buckets, nearest indices
@perf: hotpath; allocation growth and fill cost scale with active combatants
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs
@config: HashCellSize, UseSoARegistry
@assets: none
@notes: this layer is a good future seam for deeper SoA extraction
*/

using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Game.Presentation.View;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-UNITCOMBATJOBSCHEDULER-BUFFERS]
// Logical block: UnitCombatJobScheduler buffer management extraction.

namespace Game.Presentation.Performance
{
    public partial class UnitCombatJobScheduler
    {
        // [UCJS-03]
        // Native buffer growth, hash fill, snapshot ingestion, and applyback to UnitCombat.
        private void EnsureCapacity(ref Buffer buf, int count)
        {
            if (count <= buf.Capacity && buf.Buckets.IsCreated && buf.BucketCapacity >= count * 2)
            {
                return;
            }

            DisposeBuffer(ref buf);
            buf.Capacity = Mathf.NextPowerOfTwo(count);
            buf.Positions = new NativeArray<Vector3>(buf.Capacity, Allocator.Persistent);
            buf.Factions = new NativeArray<int>(buf.Capacity, Allocator.Persistent);
            buf.Nearest = new NativeArray<int>(buf.Capacity, Allocator.Persistent);
            buf.Cells = new NativeArray<int2>(buf.Capacity, Allocator.Persistent);
            buf.BucketCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, count * 2));
            buf.Buckets = new NativeParallelMultiHashMap<int, int>(buf.BucketCapacity, Allocator.Persistent);
        }

        private bool FillArrays(ref Buffer buf, List<UnitCombat> units, int count)
        {
            if (!buf.Positions.IsCreated || !buf.Factions.IsCreated || !buf.Nearest.IsCreated || !buf.Cells.IsCreated || !buf.Buckets.IsCreated)
            {
                return false;
            }

            buf.Buckets.Clear();
            for (int i = 0; i < count; i++)
            {
                var unit = units[i];
                var position = unit != null ? unit.transform.position : Vector3.zero;
                buf.Positions[i] = position;
                buf.Factions[i] = unit != null ? (int)unit.Faction : -1;
                buf.Nearest[i] = -1;
                var cell = ToCell(position, HashCellSize);
                buf.Cells[i] = cell;
                buf.Buckets.Add(HashKey(cell.x, cell.y), i);
            }

            return true;
        }

        private bool FillArraysFromSnapshot(ref Buffer buf, UnitSoARegistry.CombatSnapshot snapshot, List<int> indices, int count)
        {
            if (!buf.Positions.IsCreated || !buf.Factions.IsCreated || !buf.Nearest.IsCreated || !buf.Cells.IsCreated || !buf.Buckets.IsCreated)
            {
                return false;
            }

            if (!snapshot.Positions.IsCreated || !snapshot.Factions.IsCreated || indices == null || indices.Count < count)
            {
                return false;
            }

            buf.Buckets.Clear();
            for (int i = 0; i < count; i++)
            {
                var snapshotIndex = indices[i];
                var position2 = snapshot.Positions[snapshotIndex];
                var position = new Vector3(position2.x, position2.y, 0f);
                buf.Positions[i] = position;
                buf.Factions[i] = snapshot.Factions[snapshotIndex];
                buf.Nearest[i] = -1;
                var cell = ToCell(position, HashCellSize);
                buf.Cells[i] = cell;
                buf.Buckets.Add(HashKey(cell.x, cell.y), i);
            }

            return true;
        }

        private void ApplyResults(int bufferIndex)
        {
            if (bufferIndex < 0 || bufferIndex >= _buffers.Length)
            {
                return;
            }

            ref var buf = ref _buffers[bufferIndex];
            var count = buf.Count;
            if (count <= 0)
            {
                return;
            }

            var units = _unitBuffers[bufferIndex];
            for (int i = 0; i < count; i++)
            {
                var unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                var nearestIndex = buf.Nearest[i];
                var target = nearestIndex >= 0 && nearestIndex < count ? units[nearestIndex] : null;
                unit.SetJobNearest(target);
            }

            units.Clear();
            _indexBuffers[bufferIndex].Clear();
        }

        private void DisposeBuffers()
        {
            for (int i = 0; i < _buffers.Length; i++)
            {
                DisposeBuffer(ref _buffers[i]);
            }
        }

        private static void DisposeBuffer(ref Buffer buf)
        {
            if (buf.Positions.IsCreated) { buf.Positions.Dispose(); buf.Positions = default; }
            if (buf.Factions.IsCreated) { buf.Factions.Dispose(); buf.Factions = default; }
            if (buf.Nearest.IsCreated) { buf.Nearest.Dispose(); buf.Nearest = default; }
            if (buf.Cells.IsCreated) { buf.Cells.Dispose(); buf.Cells = default; }
            if (buf.Buckets.IsCreated) { buf.Buckets.Dispose(); buf.Buckets = default; }
            buf.Capacity = 0;
            buf.BucketCapacity = 0;
            buf.Count = 0;
        }

        private static int HashKey(int x, int y)
        {
            unchecked
            {
                var hash = 73856093 ^ x;
                hash = (hash * 19349663) ^ y;
                return hash;
            }
        }

        private static int2 ToCell(Vector3 position, float cellSize)
        {
            var inv = cellSize > 0.0001f ? 1f / cellSize : 1f;
            var x = Mathf.FloorToInt(position.x * inv);
            var y = Mathf.FloorToInt(position.y * inv);
            return new int2(x, y);
        }
    }
}
