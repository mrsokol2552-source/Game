/*
@file: My project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Buffers.cs
@module: presentation.performance.unitsoa
@purpose: Extracted Native buffer ownership and capacity helpers for UnitSoARegistry.
@entry: USOA-04
@api: UnitSoARegistry partial buffer helpers
@deps: Unity.Collections, Unity.Mathematics
@data: persistent NativeArray ownership for ORCA and combat snapshots
@perf: allocation-sensitive; capacity growth should stay amortized and deterministic
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs
@config: none directly
@assets: none
@notes: keep disposal centralized here so future snapshot backends can swap without touching lifecycle code
*/

using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-UNITSOAREGISTRY-BUFFERS]
// Logical block: UnitSoARegistry buffer ownership extraction.

namespace Game.Presentation.Performance
{
    public partial class UnitSoARegistry
    {
        // [USOA-04]
        // Capacity growth, NativeArray creation/disposal, and shared cell projection helpers.
        private void EnsureCapacity(int needed)
        {
            if (needed <= _capacity && AreArraysCreated()) return;

            DisposeArrays();
            _capacity = Mathf.NextPowerOfTwo(Mathf.Max(4, needed));
            _positions = new NativeArray<float2>(_capacity, Allocator.Persistent);
            _velocities = new NativeArray<float2>(_capacity, Allocator.Persistent);
            _preferred = new NativeArray<float2>(_capacity, Allocator.Persistent);
            _maxSpeed = new NativeArray<float>(_capacity, Allocator.Persistent);
            _hasDestination = new NativeArray<byte>(_capacity, Allocator.Persistent);
            _useOrca = new NativeArray<byte>(_capacity, Allocator.Persistent);
            _responsibility = new NativeArray<float>(_capacity, Allocator.Persistent);
            _factions = new NativeArray<int>(_capacity, Allocator.Persistent);
            _cells = new NativeArray<int2>(_capacity, Allocator.Persistent);
            _hasCombat = new NativeArray<byte>(_capacity, Allocator.Persistent);
            _isInSquad = new NativeArray<byte>(_capacity, Allocator.Persistent);
        }

        private bool AreArraysCreated()
        {
            return _positions.IsCreated
                && _velocities.IsCreated
                && _preferred.IsCreated
                && _maxSpeed.IsCreated
                && _hasDestination.IsCreated
                && _useOrca.IsCreated
                && _responsibility.IsCreated
                && _factions.IsCreated
                && _cells.IsCreated
                && _hasCombat.IsCreated
                && _isInSquad.IsCreated;
        }

        private void DisposeArrays()
        {
            if (_positions.IsCreated) { _positions.Dispose(); _positions = default; }
            if (_velocities.IsCreated) { _velocities.Dispose(); _velocities = default; }
            if (_preferred.IsCreated) { _preferred.Dispose(); _preferred = default; }
            if (_maxSpeed.IsCreated) { _maxSpeed.Dispose(); _maxSpeed = default; }
            if (_hasDestination.IsCreated) { _hasDestination.Dispose(); _hasDestination = default; }
            if (_useOrca.IsCreated) { _useOrca.Dispose(); _useOrca = default; }
            if (_responsibility.IsCreated) { _responsibility.Dispose(); _responsibility = default; }
            if (_factions.IsCreated) { _factions.Dispose(); _factions = default; }
            if (_cells.IsCreated) { _cells.Dispose(); _cells = default; }
            if (_hasCombat.IsCreated) { _hasCombat.Dispose(); _hasCombat = default; }
            if (_isInSquad.IsCreated) { _isInSquad.Dispose(); _isInSquad = default; }
            _capacity = 0;
            _count = 0;
        }

        private static int2 ToCell(Vector3 pos, float cellSize)
        {
            float inv = cellSize > 0.0001f ? 1f / cellSize : 1f;
            int x = Mathf.FloorToInt(pos.x * inv);
            int y = Mathf.FloorToInt(pos.y * inv);
            return new int2(x, y);
        }
    }
}
