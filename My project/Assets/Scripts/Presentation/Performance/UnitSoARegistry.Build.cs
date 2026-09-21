/*
@file: My project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Build.cs
@module: presentation.performance.unitsoa
@purpose: Extracted snapshot-build logic for UnitSoARegistry.
@entry: USOA-03
@api: UnitSoARegistry partial snapshot builder
@deps: UnitView, UnitCombat, Unity.Mathematics
@data: active unit lists and SoA NativeArray writes
@perf: hotpath; per-frame gather and projection cost scales with active units
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs
@config: OrcaCellSize, OrcaMinResponsibility
@assets: none
@notes: this is a clean seam for a future dedicated data-extraction layer feeding GPU or job systems
*/

using Game.Presentation.View;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-UNITSOAREGISTRY-BUILD]
// Logical block: UnitSoARegistry snapshot build extraction.

namespace Game.Presentation.Performance
{
    public partial class UnitSoARegistry
    {
        // [USOA-03]
        // Per-frame SoA snapshot build from active UnitView and UnitCombat instances.
        private void BuildSnapshot()
        {
            _units.Clear();
            _combats.Clear();
            int needed = UnitView.All.Count;
            if (needed <= 0)
            {
                _count = 0;
                return;
            }

            EnsureCapacity(needed);
            if (_units.Capacity < needed) _units.Capacity = needed;
            if (_combats.Capacity < needed) _combats.Capacity = needed;

            float cellSize = OrcaCellSize;
            float minResp = Mathf.Max(0f, OrcaMinResponsibility);
            int count = 0;
            foreach (var uv in UnitView.All)
            {
                if (uv == null || !uv.isActiveAndEnabled) continue;
                _units.Add(uv);
                var combat = uv.GetComponent<UnitCombat>();
                _combats.Add(combat);

                var pos3 = uv.transform.position;
                _positions[count] = new float2(pos3.x, pos3.y);

                var movement = uv.GetMovementSettings();
                _maxSpeed[count] = movement.MaxSpeed;

                Vector3 lastDir3 = uv.GetLastDirection();
                float2 lastDir = new float2(lastDir3.x, lastDir3.y);
                float speed = uv.GetSpeed();
                _velocities[count] = lastDir * speed;

                if (uv.TryGetDestination(out var dest))
                {
                    _hasDestination[count] = 1;
                    float2 to = new float2(dest.x - pos3.x, dest.y - pos3.y);
                    float len = math.length(to);
                    float2 dir = len > 0.0001f ? (to / len) : new float2(1f, 0f);
                    _preferred[count] = dir * movement.MaxSpeed;
                }
                else
                {
                    _hasDestination[count] = 0;
                    _preferred[count] = default;
                }

                _useOrca[count] = uv.UseOrcaVelocity ? (byte)1 : (byte)0;
                float priority = Mathf.Clamp01(uv.OrcaPriority);
                _responsibility[count] = Mathf.Max(minResp, Mathf.Max(0f, 1f - priority));

                _factions[count] = combat != null ? (int)combat.Faction : 0;
                _hasCombat[count] = combat != null ? (byte)1 : (byte)0;
                _isInSquad[count] = combat != null && combat.IsInSquad ? (byte)1 : (byte)0;

                _cells[count] = ToCell(pos3, cellSize);
                count++;
            }

            _count = count;
        }
    }
}
