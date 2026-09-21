/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Search.cs
@module: presentation.pathfinding.crowding
@purpose: Extracted free-cell search and ring-enumeration helpers for CrowdingResolver.
@entry: CROWD-04
@api: CrowdingResolver partial search helpers
@deps: HexPathfindingBootstrap, OccupancyHash, PathManager
@data: candidate free-cell lists and reservation keys
@perf: bounded by SearchRadius and MaxSlotsPerGroup; sensitive to stack density
@thread: main thread only
@tests: manual overlap-stack verification
@config: SearchRadius, MaxSlotsPerGroup
@assets: none
@notes: ring search intentionally remains deterministic except for minor jitter used to break ties
*/

using System.Collections.Generic;
using Game.Presentation.View;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-CROWDINGRESOLVER-SEARCH]
// Logical block: CrowdingResolver free-cell search extraction.

namespace Game.Presentation.Pathfinding
{
    public partial class CrowdingResolver
    {
        // [CROWD-04]
        // Free-cell search, reservation-key helpers, and odd-r ring enumeration around crowded cells.
        private List<Vector2Int> GatherFreeCells(HexPathfindingBootstrap hex, Vector2Int center, int radius, HashSet<int> reserved, UnitView self)
        {
            var result = new List<Vector2Int>();
            var candidates = new List<(Vector2Int cell, float score)>();
            Vector3 centerWorld = hex.GridToWorld(center.x, center.y);
            for (int ring = 1; ring <= Mathf.Max(1, radius); ring++)
            {
                foreach (var cell in HexRing(center, ring))
                {
                    if (cell.x < 0 || cell.y < 0 || cell.x >= hex.Width || cell.y >= hex.Height) continue;

                    int key = Key(cell);
                    if (reserved.Contains(key)) continue;
                    if (!hex.IsWalkable(cell.x, cell.y)) continue;

                    if (_occ != null)
                    {
                        Vector3 occupiedWorld = hex.GridToWorld(cell.x, cell.y);
                        if (_occ.IsOccupied(occupiedWorld, self, enemiesOnly: false)) continue;
                    }
                    else if (_pm != null && _pm.IsCellOccupied(cell, self, enemiesOnly: false))
                    {
                        continue;
                    }

                    Vector3 world = hex.GridToWorld(cell.x, cell.y);
                    float distSq = (world - centerWorld).sqrMagnitude;
                    float jitter = UnityEngine.Random.value * 0.01f;
                    candidates.Add((cell, distSq + jitter));
                }
            }

            candidates.Sort((a, b) => a.score.CompareTo(b.score));
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2Int cell = candidates[i].cell;
                int key = Key(cell);
                if (reserved.Contains(key)) continue;
                reserved.Add(key);
                result.Add(cell);
                if (MaxSlotsPerGroup > 0 && result.Count >= MaxSlotsPerGroup) break;
            }

            return result;
        }

        private static int Key(Vector2Int cell) => (cell.y << 16) ^ (cell.x & 0xFFFF);

        private IEnumerable<Vector2Int> HexRing(Vector2Int center, int radius)
        {
            var dirs = new (int q, int r)[] { (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1) };
            int centerQ = center.x - (center.y - (center.y & 1)) / 2;
            int centerR = center.y;
            int axialQ = centerQ + dirs[4].q * radius;
            int axialR = centerR + dirs[4].r * radius;
            for (int side = 0; side < 6; side++)
            {
                for (int step = 0; step < radius; step++)
                {
                    var dir = dirs[side];
                    axialQ += dir.q;
                    axialR += dir.r;
                    int col = axialQ + (axialR - (axialR & 1)) / 2;
                    int row = axialR;
                    yield return new Vector2Int(col, row);
                }
            }
        }
    }
}
