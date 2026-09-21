/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/PathManager.Occupancy.cs
@module: presentation.pathfinding.paths.occupancy
@purpose: Maintains per-frame occupancy caches and occupied-path rejection logic for PathManager.
@entry: PMGR-03, PathManager.BuildOccupied
@api: partial of PathManager used internally by sync path builds and occupancy-aware path validation
@deps: HexPathfindingBootstrap, UnitCombat
@data: cached occupied sets, recent-friendly TTL maps, stale-key scratch list
@perf: hotpath, runs during path requests and should stay allocation-light
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual path/build validation
@config: FriendlyReserveSeconds, UseOccupancyHash, UseStaticObstacleHash
@assets: none directly
@notes: returns cached occupancy sets when possible to avoid rebuilding transient hashes per path request
*/

using System.Collections.Generic;
using Game.Infrastructure.AI.Pathfinding;
using Game.Presentation.View;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER-OCCUPANCY]
// Logical block: Scripts/Presentation/Pathfinding/PathManager.Occupancy.

namespace Game.Presentation.Pathfinding
{
    public partial class PathManager
    {
        // [PMGR-03]
        // Occupancy-cache maintenance for friendly/enemy-aware path reuse and avoidance.
        private HashSet<int> BuildOccupied(System.Func<Vector3, Vector2Int> worldToCell, UnitView self, bool enemiesOnly, float friendTtl = 0f)
        {
            CacheBootstraps();
            int gridId = _hexCached != null ? _hexCached.GetInstanceID() : (_gridCached != null ? _gridCached.GetInstanceID() : 0);
            EnsureOccupiedCache(worldToCell, gridId, friendTtl);

            Game.Domain.Units.Faction? selfFaction = null;
            if (self != null) selfFaction = self.GetComponent<Game.Presentation.View.UnitCombat>()?.Faction;

            if (enemiesOnly)
            {
                if (selfFaction == Game.Domain.Units.Faction.Player) return _occupiedEnemiesCache;
                if (selfFaction == Game.Domain.Units.Faction.Enemy) return _occupiedPlayersCache;
                return _occupiedAllCache;
            }

            if (friendTtl > 0f)
            {
                if (selfFaction == Game.Domain.Units.Faction.Player) return _occupiedAllWithRecentPlayersCache;
                if (selfFaction == Game.Domain.Units.Faction.Enemy) return _occupiedAllWithRecentEnemiesCache;
            }

            return _occupiedAllCache;
        }

        private void EnsureOccupiedCache(System.Func<Vector3, Vector2Int> worldToCell, int gridId, float friendTtl)
        {
            int frame = Time.frameCount;
            if (frame == _occupiedCacheFrame && gridId == _occupiedCacheGridId) return;

            _occupiedCacheFrame = frame;
            _occupiedCacheGridId = gridId;
            _occupiedAllCache.Clear();
            _occupiedPlayersCache.Clear();
            _occupiedEnemiesCache.Clear();

            bool trackRecent = friendTtl > 0f;
            if (trackRecent)
            {
                _occupiedAllWithRecentPlayersCache.Clear();
                _occupiedAllWithRecentEnemiesCache.Clear();
            }

            float now = Time.time;
            foreach (var uc in Game.Presentation.View.UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                var cell = worldToCell(uc.transform.position);
                int key = Key(cell.x, cell.y);
                _occupiedAllCache.Add(key);

                if (uc.Faction == Game.Domain.Units.Faction.Player)
                {
                    _occupiedPlayersCache.Add(key);
                    if (trackRecent) _friendRecentPlayers[key] = now;
                }
                else if (uc.Faction == Game.Domain.Units.Faction.Enemy)
                {
                    _occupiedEnemiesCache.Add(key);
                    if (trackRecent) _friendRecentEnemies[key] = now;
                }
            }

            if (trackRecent)
            {
                _occupiedAllWithRecentPlayersCache.UnionWith(_occupiedAllCache);
                _occupiedAllWithRecentEnemiesCache.UnionWith(_occupiedAllCache);
                AddRecent(_friendRecentPlayers, _occupiedAllWithRecentPlayersCache, now, friendTtl);
                AddRecent(_friendRecentEnemies, _occupiedAllWithRecentEnemiesCache, now, friendTtl);
            }
        }

        private void AddRecent(Dictionary<int, float> recent, HashSet<int> output, float now, float ttl)
        {
            if (recent.Count == 0) return;
            _staleRecentKeys.Clear();
            float pruneAfter = ttl * 4f;
            foreach (var kv in recent)
            {
                float age = now - kv.Value;
                if (age <= ttl)
                    output.Add(kv.Key);
                else if (age > pruneAfter)
                    _staleRecentKeys.Add(kv.Key);
            }

            for (int i = 0; i < _staleRecentKeys.Count; i++)
                recent.Remove(_staleRecentKeys[i]);
        }

        private void ReleaseOccupied(HashSet<int> occupied)
        {
            if (occupied == null) return;
            if (IsCachedOccupied(occupied)) return;
            ReturnHashSet(occupied);
        }

        private bool IsCachedOccupied(HashSet<int> occupied)
        {
            return ReferenceEquals(occupied, _occupiedAllCache)
                || ReferenceEquals(occupied, _occupiedPlayersCache)
                || ReferenceEquals(occupied, _occupiedEnemiesCache)
                || ReferenceEquals(occupied, _occupiedAllWithRecentPlayersCache)
                || ReferenceEquals(occupied, _occupiedAllWithRecentEnemiesCache);
        }

        private bool PathHitsOccupied(List<GridPoint> path, HashSet<int> occupied, Vector2Int from)
        {
            if (occupied == null || occupied.Count == 0) return false;
            for (int i = 0; i < path.Count; i++)
            {
                var gp = path[i];
                if (gp.X == from.x && gp.Y == from.y) continue;
                if (occupied.Contains(Key(gp.X, gp.Y))) return true;
            }
            return false;
        }
    }
}
