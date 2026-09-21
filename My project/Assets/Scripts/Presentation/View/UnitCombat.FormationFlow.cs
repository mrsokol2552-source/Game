/*
@file: My project/Assets/Scripts/Presentation/View/UnitCombat.FormationFlow.cs
@module: presentation.combat.unit.formation_flow
@purpose: Holds squad metadata, formation slot math, shared hex access, and flow-field steering for UnitCombat.
@entry: UCOM-04, UCOM-08, UnitCombat.TryFlowFieldMove
@api: partial class implementation for UnitCombat
@deps: FlowFieldManager, HexPathfindingBootstrap, UnitPathFollower, UnitView
@data: squad ids, squad modes, formation indices, flow-field timers, repath frame budget coordination
@perf: hotpath helper layer used from UnitCombat.Update; avoid allocations and repeated scene lookups
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual squad battle verification
@config: UseFlowFields, FlowFieldMinDistance, FlowFieldStepInterval, FlowFieldStepJitter, formation offset inspector fields
@assets: none directly
@notes: keep squad metadata and flow steering together so future macro/micro movement refactors have a single seam
*/

using Game.Presentation.Pathfinding;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-FORMATIONFLOW]
// Logical block: Scripts/Presentation/View/UnitCombat.FormationFlow.

namespace Game.Presentation.View
{
    public partial class UnitCombat
    {
        // [UCOM-04]
        // Squad assignment and per-unit formation metadata managed by the squad system.
        public void SetSquad(int squadId, SquadMode mode)
        {
            _squadId = squadId;
            _squadMode = mode;
        }

        public void SetSquadMode(SquadMode mode)
        {
            _squadMode = mode;
        }

        public void SetFormationIndex(int index)
        {
            _formationIndex = index;
        }

        public void ClearSquad()
        {
            _squadId = 0;
            _squadMode = SquadMode.None;
            _formationIndex = -1;
        }

        // [UCOM-08]
        // Far-distance steering via shared flow fields before falling back to per-unit path requests.
        private bool TryFlowFieldMove(Vector3 desired, float targetDist, bool pathActive, bool ignoreMinDistance)
        {
            if (!UseFlowFields) return false;
            if (!ignoreMinDistance && targetDist < FlowFieldMinDistance) return false;
            var mgr = FlowFieldManager.Instance;
            if (mgr == null || !mgr.Enabled) return false;

            if (_usingFlowField)
            {
                if (_flowFieldTimer > 0f)
                {
                    _flowFieldTimer -= Time.deltaTime;
                    if (_flowFieldTimer > 0f && _view != null && _view.TryGetDestination(out _))
                        return true;
                }
            }
            else if (_flowFieldTimer > 0f)
            {
                _flowFieldTimer -= Time.deltaTime;
            }

            if (!mgr.TryGetNextPoint(_tr.position, desired, Faction, out var next))
                return false;

            InvalidatePendingPath();
            if (pathActive && _follower != null && _follower.Source == UnitPathFollower.PathSource.Combat)
                _follower.Cancel();
            _pathPending = false;
            _view.SetDestination(next);
            _combatSteering = true;
            _usingFlowField = true;
            _flowFieldTimer = FlowFieldStepInterval + Random.Range(0f, FlowFieldStepJitter);
            return true;
        }

        private static void TouchBudgetFrame()
        {
            if (RepathBudgetPerFrame <= 0)
                return;
            int frame = Time.frameCount;
            if (frame == _budgetFrame) return;
            _budgetFrame = frame;
            _repathsThisFrame = 0;
        }

        private static HexPathfindingBootstrap SharedHex()
        {
            if (_sharedHex == null || !_sharedHex.isActiveAndEnabled)
                _sharedHex = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            return _sharedHex;
        }

        private struct Axial
        {
            public int q;
            public int r;

            public Axial(int q, int r)
            {
                this.q = q;
                this.r = r;
            }
        }

        private static Axial OddRToAxial(Vector2Int cell)
        {
            int q = cell.x - (cell.y - (cell.y & 1)) / 2;
            int r = cell.y;
            return new Axial(q, r);
        }

        private static Vector2Int AxialToOddR(Axial axial)
        {
            int col = axial.q + (axial.r - (axial.r & 1)) / 2;
            int row = axial.r;
            return new Vector2Int(col, row);
        }

        private static Axial FormationAxialOffset(int index, int spacing, int maxRadius)
        {
            if (index <= 0) return new Axial(0, 0);
            int n = index - 1;
            int ring = 1;
            while (n >= 6 * ring)
            {
                n -= 6 * ring;
                ring++;
            }

            if (maxRadius > 0)
                ring = Mathf.Min(ring, maxRadius);

            int side = ring > 0 ? n / ring : 0;
            int offset = ring > 0 ? n % ring : 0;
            var dirs = new Axial[]
            {
                new Axial(1, 0),
                new Axial(1, -1),
                new Axial(0, -1),
                new Axial(-1, 0),
                new Axial(-1, 1),
                new Axial(0, 1)
            };
            Axial pos = new Axial(ring, 0);
            for (int i = 0; i < side; i++)
            {
                pos.q += dirs[i].q * ring;
                pos.r += dirs[i].r * ring;
            }

            pos.q += dirs[side].q * offset;
            pos.r += dirs[side].r * offset;
            pos.q *= spacing;
            pos.r *= spacing;
            return pos;
        }
    }
}
