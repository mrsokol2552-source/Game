/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.TileGraph.cs
@module: presentation.pathfinding.flowfields.tilegraph
@purpose: Maintains the coarse tile graph used to gate flow-field work to relevant macro navigation corridors.
@entry: FFLD-06
@api: internal partial of FlowFieldManager
@deps: HexPathfindingBootstrap, tile buffers from FlowFieldManager main file
@data: tile adjacency graph, tile BFS buffers, active tile expansions
@perf: hotpath, rebuilt when tile size or walkability version changes
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual squad movement verification
@config: TileSize, TilePadding, UseTiledFields
@assets: none
@notes: This layer is the seam between coarse macro routing and per-cell flow-field integration.
*/

using System;
using System.Collections.Generic;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER-TILEGRAPH]
// Logical block: Scripts/Presentation/Pathfinding/FlowFieldManager.TileGraph.

namespace Game.Presentation.Pathfinding
{
    public partial class FlowFieldManager
    {
        // [FFLD-06]
        // Construction and maintenance of the coarse tile graph used for macro navigation.
        private bool EnsureTileGraph()
        {
            if (!UseTiledFields) return false;
            if (_hex == null) return false;
            int tileSize = Mathf.Max(1, TileSize);
            if (_tileNeighbors == null ||
                _tileGraphWalkableVersion != _hex.WalkableVersion ||
                _tileGraphTileSize != tileSize ||
                _tileGraphWidth != _hex.Width ||
                _tileGraphHeight != _hex.Height)
            {
                BuildTileGraph(tileSize);
            }
            return _tileNeighbors != null && _tileNeighbors.Length == _tileCount && _tileCount > 0;
        }

        private void BuildTileGraph(int tileSize)
        {
            if (_hex == null) return;
            _tileGraphWalkableVersion = _hex.WalkableVersion;
            _tileGraphTileSize = tileSize;
            _tileGraphWidth = _hex.Width;
            _tileGraphHeight = _hex.Height;
            _tileCols = Mathf.CeilToInt(_hex.Width / (float)tileSize);
            _tileRows = Mathf.CeilToInt(_hex.Height / (float)tileSize);
            _tileCount = Mathf.Max(1, _tileCols * _tileRows);
            _tileNeighbors = new List<int>[_tileCount];
            _tileHasWalkable = new bool[_tileCount];
            for (int i = 0; i < _tileCount; i++)
                _tileNeighbors[i] = new List<int>(6);

            EnsureTileBuffers();

            var walkable = _hex.GetWalkableNative();
            int width = _hex.Width;
            int height = _hex.Height;
            int max = width * height;
            if (!walkable.IsCreated || walkable.Length != max)
            {
                for (int i = 0; i < _tileCount; i++)
                    _tileHasWalkable[i] = true;
                return;
            }

            for (int row = 0; row < height; row++)
            {
                var offs = (row & 1) == 0 ? EvenOffsets : OddOffsets;
                for (int col = 0; col < width; col++)
                {
                    int idx = row * width + col;
                    if (walkable[idx] == 0) continue;
                    int tile = TileIndex(col, row, tileSize);
                    if (tile < 0 || tile >= _tileCount) continue;
                    _tileHasWalkable[tile] = true;

                    for (int i = 0; i < 6; i++)
                    {
                        int nc = col + offs[i].x;
                        int nr = row + offs[i].y;
                        if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                        int nIdx = nr * width + nc;
                        if (walkable[nIdx] == 0) continue;
                        int nTile = TileIndex(nc, nr, tileSize);
                        if (nTile == tile || nTile < 0 || nTile >= _tileCount) continue;
                        AddNeighbor(tile, nTile);
                        AddNeighbor(nTile, tile);
                    }
                }
            }
        }

        private void EnsureTileBuffers()
        {
            if (_tileCount <= 0) return;
            if (_tilePrev == null || _tilePrev.Length != _tileCount)
            {
                _tilePrev = new int[_tileCount];
                _tileVisit = new int[_tileCount];
                _tileQueue = new int[_tileCount];
                _tilePadVisit = new int[_tileCount];
                _tilePadDist = new int[_tileCount];
            }
        }

        private void AddNeighbor(int tile, int neighbor)
        {
            var list = _tileNeighbors[tile];
            if (list == null) return;
            if (!list.Contains(neighbor))
                list.Add(neighbor);
        }

        private bool TryGetTilePath(Vector2Int fromCell, Vector2Int targetCell, List<int> path)
        {
            path.Clear();
            if (_tileNeighbors == null || _tileCount <= 0) return false;
            int tileSize = Mathf.Max(1, TileSize);
            int fromTile = TileIndex(fromCell.x, fromCell.y, tileSize);
            int targetTile = TileIndex(targetCell.x, targetCell.y, tileSize);
            if (fromTile < 0 || targetTile < 0 || fromTile >= _tileCount || targetTile >= _tileCount)
                return false;
            if (_tileHasWalkable != null)
            {
                if (!_tileHasWalkable[fromTile] || !_tileHasWalkable[targetTile])
                    return false;
            }
            if (fromTile == targetTile)
            {
                path.Add(fromTile);
                return true;
            }

            EnsureTileBuffers();
            int stamp = ++_tileVisitStamp;
            if (stamp == int.MaxValue)
            {
                Array.Clear(_tileVisit, 0, _tileVisit.Length);
                _tileVisitStamp = 1;
                stamp = 1;
            }

            int head = 0;
            int tail = 0;
            _tileQueue[tail++] = fromTile;
            _tileVisit[fromTile] = stamp;
            _tilePrev[fromTile] = -1;

            bool found = false;
            while (head < tail)
            {
                int tile = _tileQueue[head++];
                if (tile == targetTile)
                {
                    found = true;
                    break;
                }
                var neighbors = _tileNeighbors[tile];
                if (neighbors == null) continue;
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int n = neighbors[i];
                    if (_tileHasWalkable != null && !_tileHasWalkable[n]) continue;
                    if (_tileVisit[n] == stamp) continue;
                    _tileVisit[n] = stamp;
                    _tilePrev[n] = tile;
                    _tileQueue[tail++] = n;
                }
            }

            if (!found) return false;
            int cur = targetTile;
            while (cur != -1)
            {
                path.Add(cur);
                cur = _tilePrev[cur];
            }
            path.Reverse();
            return true;
        }

        private void ExpandTilePath(List<int> path, int padding, List<int> output)
        {
            output.Clear();
            if (path == null || path.Count == 0) return;
            if (padding <= 0 || _tileNeighbors == null)
            {
                output.AddRange(path);
                return;
            }

            EnsureTileBuffers();
            int stamp = ++_tilePadStamp;
            if (stamp == int.MaxValue)
            {
                Array.Clear(_tilePadVisit, 0, _tilePadVisit.Length);
                _tilePadStamp = 1;
                stamp = 1;
            }

            int head = 0;
            int tail = 0;
            for (int i = 0; i < path.Count; i++)
            {
                int tile = path[i];
                if (tile < 0 || tile >= _tileCount) continue;
                if (_tilePadVisit[tile] == stamp) continue;
                _tilePadVisit[tile] = stamp;
                _tilePadDist[tile] = 0;
                _tileQueue[tail++] = tile;
                output.Add(tile);
            }

            while (head < tail)
            {
                int tile = _tileQueue[head++];
                int dist = _tilePadDist[tile];
                if (dist >= padding) continue;
                var neighbors = _tileNeighbors[tile];
                if (neighbors == null) continue;
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int n = neighbors[i];
                    if (_tilePadVisit[n] == stamp) continue;
                    _tilePadVisit[n] = stamp;
                    _tilePadDist[n] = dist + 1;
                    _tileQueue[tail++] = n;
                    output.Add(n);
                }
            }
        }

        private int TileIndex(int col, int row, int tileSize)
        {
            int tx = col / tileSize;
            int ty = row / tileSize;
            return ty * _tileCols + tx;
        }
    }
}
