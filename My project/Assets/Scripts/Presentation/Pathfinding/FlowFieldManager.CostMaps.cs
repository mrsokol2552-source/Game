/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.CostMaps.cs
@module: presentation.pathfinding.flowfields.costmaps
@purpose: Maintains crowd and influence cost maps used by FlowFieldManager to bias shared movement fields.
@entry: FFLD-04, FFLD-05, FlowFieldManager.BuildCrowdInfo, FlowFieldManager.BuildInfluenceInfo
@api: partial of FlowFieldManager used internally by update ticks and flow queries
@deps: HexPathfindingBootstrap, UnitCombat, Faction
@data: crowd counts, influence values, stamps, axial offsets
@perf: hotpath, these maps refresh during gameplay and directly affect shared field build cost
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual squad movement verification
@config: UseCrowdCosts, UseInfluenceCosts, Crowd*, Influence*, VectorStepFraction
@assets: none directly
@notes: cost-map logic is isolated to keep future data-extraction and GPU migration seams clean
*/

using System;
using System.Collections.Generic;
using Game.Domain.Units;
using Game.Presentation.View;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER-COSTMAPS]
// Logical block: Scripts/Presentation/Pathfinding/FlowFieldManager.CostMaps.

namespace Game.Presentation.Pathfinding
{
    public partial class FlowFieldManager
    {
        private struct CrowdCostInfo
        {
            public bool Enabled;
            public int[] Count;
            public int[] Stamp;
            public int StampValue;
            public int Width;
            public int Height;
            public int MinUnits;
            public int CostPerUnit;
            public int MaxCost;
            public int DetourAllowance;
        }

        private struct InfluenceCostInfo
        {
            public bool Enabled;
            public int[] Values;
            public int[] Stamp;
            public int StampValue;
            public int Width;
            public int Height;
            public int MaxCost;
            public int DetourAllowance;
        }

        private struct AxialOffset
        {
            public int q;
            public int r;
            public int weight;

            public AxialOffset(int q, int r, int weight)
            {
                this.q = q;
                this.r = r;
                this.weight = weight;
            }
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

        // [FFLD-04]
        // Dynamic crowd-density sampling that feeds penalties into integration fields.
        private void UpdateCrowdMap()
        {
            if (_hex == null) return;
            if (!UseCrowdCosts) return;
            int size = _hex.Width * _hex.Height;
            if (size <= 0) return;
            if (_crowdCount == null || _crowdCount.Length != size)
            {
                _crowdCount = new int[size];
                _crowdStamp = new int[size];
                _crowdStampValue = 1;
            }

            int stamp = ++_crowdStampValue;
            if (stamp == int.MaxValue)
            {
                Array.Clear(_crowdStamp, 0, _crowdStamp.Length);
                _crowdStampValue = 1;
                stamp = 1;
            }

            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                var cell = _hex.WorldToGrid(uc.transform.position);
                if (cell.x < 0 || cell.y < 0 || cell.x >= _hex.Width || cell.y >= _hex.Height) continue;
                int idx = cell.y * _hex.Width + cell.x;
                if (_crowdStamp[idx] != stamp)
                {
                    _crowdStamp[idx] = stamp;
                    _crowdCount[idx] = 1;
                }
                else
                {
                    _crowdCount[idx]++;
                }
            }

            _crowdStampValue = stamp;
        }

        private CrowdCostInfo BuildCrowdInfo()
        {
            if (!UseCrowdCosts || _hex == null || _crowdCount == null || _crowdStamp == null)
            {
                return new CrowdCostInfo { Enabled = false };
            }
            return new CrowdCostInfo
            {
                Enabled = UseCrowdCosts,
                Count = _crowdCount,
                Stamp = _crowdStamp,
                StampValue = _crowdStampValue,
                Width = _hex.Width,
                Height = _hex.Height,
                MinUnits = Mathf.Max(0, CrowdMinUnits),
                CostPerUnit = Mathf.Max(0, CrowdCostPerUnit),
                MaxCost = CrowdCostMax,
                DetourAllowance = Mathf.Max(0, CrowdDetourAllowance)
            };
        }

        // [FFLD-05]
        // Tactical influence accumulation used to bias movement away from danger zones.
        private InfluenceCostInfo BuildInfluenceInfo(Faction faction)
        {
            if (!UseInfluenceCosts || _hex == null)
                return new InfluenceCostInfo { Enabled = false };
            if (_influenceForPlayer == null || _influenceForEnemy == null ||
                _influenceForPlayerStamp == null || _influenceForEnemyStamp == null)
                return new InfluenceCostInfo { Enabled = false };

            int[] values;
            int[] stamp;
            switch (faction)
            {
                case Faction.Player:
                    values = _influenceForPlayer;
                    stamp = _influenceForPlayerStamp;
                    break;
                case Faction.Enemy:
                    values = _influenceForEnemy;
                    stamp = _influenceForEnemyStamp;
                    break;
                default:
                    return new InfluenceCostInfo { Enabled = false };
            }

            return new InfluenceCostInfo
            {
                Enabled = true,
                Values = values,
                Stamp = stamp,
                StampValue = _influenceStampValue,
                Width = _hex.Width,
                Height = _hex.Height,
                MaxCost = InfluenceCostMax,
                DetourAllowance = Mathf.Max(0, InfluenceDetourAllowance)
            };
        }

        private void UpdateInfluenceMap()
        {
            if (_hex == null) return;
            if (!UseInfluenceCosts) return;
            int size = _hex.Width * _hex.Height;
            if (size <= 0) return;
            if (_influenceForPlayer == null || _influenceForPlayer.Length != size)
            {
                _influenceForPlayer = new int[size];
                _influenceForEnemy = new int[size];
                _influenceForPlayerStamp = new int[size];
                _influenceForEnemyStamp = new int[size];
                _influenceStampValue = 1;
            }

            int stamp = ++_influenceStampValue;
            if (stamp == int.MaxValue)
            {
                Array.Clear(_influenceForPlayerStamp, 0, _influenceForPlayerStamp.Length);
                Array.Clear(_influenceForEnemyStamp, 0, _influenceForEnemyStamp.Length);
                _influenceStampValue = 1;
                stamp = 1;
            }

            int radius = Mathf.Max(0, InfluenceRadiusHex);
            int costPerUnit = Mathf.Max(0, InfluenceCostPerUnit);
            if (radius == 0 || costPerUnit == 0)
            {
                _influenceStampValue = stamp;
                return;
            }

            EnsureInfluenceOffsets();

            int width = _hex.Width;
            int height = _hex.Height;
            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                int[] values;
                int[] stamps;
                if (uc.Faction == Faction.Player)
                {
                    values = _influenceForEnemy;
                    stamps = _influenceForEnemyStamp;
                }
                else if (uc.Faction == Faction.Enemy)
                {
                    values = _influenceForPlayer;
                    stamps = _influenceForPlayerStamp;
                }
                else
                {
                    continue;
                }

                var cell = _hex.WorldToGrid(uc.transform.position);
                if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height) continue;
                var axial = OddRToAxial(cell);
                for (int i = 0; i < _influenceOffsets.Count; i++)
                {
                    var off = _influenceOffsets[i];
                    var targetAxial = new Axial(axial.q + off.q, axial.r + off.r);
                    var targetCell = AxialToOddR(targetAxial);
                    if (targetCell.x < 0 || targetCell.y < 0 || targetCell.x >= width || targetCell.y >= height)
                        continue;
                    int idx = targetCell.y * width + targetCell.x;
                    int add = off.weight * costPerUnit;
                    if (stamps[idx] != stamp)
                    {
                        stamps[idx] = stamp;
                        values[idx] = add;
                    }
                    else
                    {
                        values[idx] += add;
                    }
                    if (InfluenceCostMax > 0 && values[idx] > InfluenceCostMax)
                        values[idx] = InfluenceCostMax;
                }
            }

            _influenceStampValue = stamp;
        }

        private static int HexDistance(Vector2Int a, Vector2Int b)
        {
            int aq = a.x - (a.y - (a.y & 1)) / 2;
            int ar = a.y;
            int bq = b.x - (b.y - (b.y & 1)) / 2;
            int br = b.y;
            int dq = aq - bq;
            int dr = ar - br;
            int ds = (aq + ar) - (bq + br);
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
        }

        private float ComputeVectorStepDistance()
        {
            if (_hex == null) return 0f;
            float neighbor = Mathf.Sqrt(3f) * _hex.HexSize;
            float frac = Mathf.Clamp01(VectorStepFraction);
            return neighbor * frac;
        }

        private void EnsureInfluenceOffsets()
        {
            int radius = Mathf.Max(0, InfluenceRadiusHex);
            if (radius == _influenceRadiusCached && _influenceOffsets.Count > 0)
                return;
            _influenceOffsets.Clear();
            _influenceRadiusCached = radius;
            if (radius <= 0) return;

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    int dist = AxialDistance(q, r);
                    int weight = radius - dist + 1;
                    if (weight <= 0) continue;
                    _influenceOffsets.Add(new AxialOffset(q, r, weight));
                }
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

        private static int AxialDistance(int q, int r)
        {
            int s = -q - r;
            return (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s)) / 2;
        }
    }
}
