/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Walkability.cs
@module: presentation.pathfinding.hexgrid
@purpose: Extracted walkability mutation, collider baking, persistence, and Native mirror maintenance for HexPathfindingBootstrap.
@entry: HPFB-04, HPFB-06
@api: HexPathfindingBootstrap partial walkability helpers
@deps: Physics2D, PathRequestQueue, NativeArray walkability mirrors
@data: mutable walkable grid and job-facing byte mirrors
@perf: memory-sensitive and touched by streaming/bake/update flows
@thread: main thread writes, jobs read the Native mirror
@tests: PlayMode/FpsStressTests and manual obstacle rebake verification
@config: ObstacleMask, SampleRadius, AutoBakeColliders, LogBake
@assets: collider layers and obstacle bake inputs
@notes: gameplay systems depend on this layer staying authoritative even when render systems change later
*/

using Unity.Collections;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP-WALKABILITY]
// Logical block: HexPathfindingBootstrap walkability/bake extraction.

namespace Game.Presentation.Pathfinding
{
    public partial class HexPathfindingBootstrap
    {
        // [HPFB-04]
        // Public editing API for walkability, collider baking, persistence, and dirty-rectangle mutation.
        public void SetBlockedAtWorld(Vector3 world, bool blocked)
        {
            var cell = WorldToGrid(world);
            SetWalkable(cell.x, cell.y, !blocked);
        }

        public void BakeFromPhysicsRect(Bounds worldBounds, int paddingCells = 1)
        {
            if (_walkable == null) return;
            var minCell = WorldToGrid(worldBounds.min);
            var maxCell = WorldToGrid(worldBounds.max);
            BakeFromPhysicsRectCells(minCell.x, minCell.y, maxCell.x, maxCell.y, paddingCells);
        }

        public void ClearAllBlocks()
        {
            if (_walkable == null) return;
            for (int row = 0; row < Height; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    _walkable[row, col] = true;
                }
            }

            MarkWalkableDirty();
        }

        public void BakeFromPhysics()
        {
            if (_walkable == null) return;

            var radius = ResolveSampleRadius();
            var maskValue = ResolveObstacleMaskValue();
            var blocked = 0;
            for (int row = 0; row < Height; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    var world = GridToWorld(col, row);
                    var hit = Physics2D.OverlapCircle(world, radius, maskValue);
                    var isWalkable = hit == null;
                    _walkable[row, col] = isWalkable;
                    if (!isWalkable)
                    {
                        blocked++;
                    }
                }
            }

            MarkWalkableDirty();
            if (LogBake)
            {
                Debug.Log($"[HexPathfinding] BakeFromPhysics: blocked={blocked} / total={Width * Height}, mask=0x{maskValue:X}");
            }
        }

        public void BakeFromPhysicsRectCells(int minCol, int minRow, int maxCol, int maxRow, int paddingCells = 1)
        {
            if (_walkable == null || Width <= 0 || Height <= 0)
            {
                return;
            }

            var pad = Mathf.Max(0, paddingCells);
            var c0 = Mathf.Clamp(Mathf.Min(minCol, maxCol) - pad, 0, Width - 1);
            var c1 = Mathf.Clamp(Mathf.Max(minCol, maxCol) + pad, 0, Width - 1);
            var r0 = Mathf.Clamp(Mathf.Min(minRow, maxRow) - pad, 0, Height - 1);
            var r1 = Mathf.Clamp(Mathf.Max(minRow, maxRow) + pad, 0, Height - 1);
            if (c0 > c1 || r0 > r1)
            {
                return;
            }

            var radius = ResolveSampleRadius();
            var maskValue = ResolveObstacleMaskValue();
            var anyChange = false;
            var blocked = 0;
            var total = 0;
            for (int row = r0; row <= r1; row++)
            {
                for (int col = c0; col <= c1; col++)
                {
                    var world = GridToWorld(col, row);
                    var hit = Physics2D.OverlapCircle(world, radius, maskValue);
                    var isWalkable = hit == null;
                    if (_walkable[row, col] != isWalkable)
                    {
                        anyChange = true;
                    }

                    _walkable[row, col] = isWalkable;
                    if (!isWalkable)
                    {
                        blocked++;
                    }

                    total++;
                }
            }

            if (anyChange)
            {
                MarkWalkableDirtyRect(c0, r0, c1, r1);
            }

            if (LogBake)
            {
                Debug.Log($"[HexPathfinding] BakeFromPhysicsRect: blocked={blocked} / total={total}, rect=({c0},{r0})-({c1},{r1}), mask=0x{maskValue:X}");
            }
        }

        [ContextMenu("Rebake Obstacles")]
        private void RebakeObstaclesInspector()
        {
            BakeFromPhysics();
        }

        public System.Collections.Generic.IEnumerable<Vector2Int> CaptureBlocked()
        {
            for (int row = 0; row < Height; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    if (_walkable != null && !_walkable[row, col])
                    {
                        yield return new Vector2Int(col, row);
                    }
                }
            }
        }

        public void RestoreBlocked(System.Collections.Generic.IEnumerable<Vector2Int> blocks)
        {
            ClearAllBlocks();
            if (blocks == null)
            {
                return;
            }

            foreach (var cell in blocks)
            {
                if (cell.y >= 0 && cell.y < Height && cell.x >= 0 && cell.x < Width)
                {
                    _walkable[cell.y, cell.x] = false;
                }
            }

            MarkWalkableDirty();
        }

        public void FitToCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            var worldWidth = HexWidth * Width * 0.75f;
            var worldHeight = HexHeight * Height * 0.5f;
            var center = cam.transform.position;
            Origin = new Vector2(center.x - worldWidth * 0.5f, center.y - worldHeight * 0.5f);
            MarkWalkableDirty();
        }

        public void SetWalkable(int col, int row, bool walkable)
        {
            if (col < 0 || row < 0 || col >= Width || row >= Height)
            {
                return;
            }

            if (_walkable[row, col] == walkable)
            {
                return;
            }

            _walkable[row, col] = walkable;
            MarkWalkableDirtyRect(col, row, col, row);
        }

        public NativeArray<byte> GetWalkableNative()
        {
            if (_nativeDirty)
            {
                UpdateNativeWalkable();
            }

            return _walkableNative;
        }

        private void UpdateNativeWalkable()
        {
            if (_walkable == null)
            {
                return;
            }

            var queue = PathRequestQueue.Instance;
            if (queue != null)
            {
                queue.CompleteActiveJobAndClear();
            }

            var length = Width * Height;
            if (_walkableNative.IsCreated)
            {
                if (_walkableNative.Length != length)
                {
                    _walkableNative.Dispose();
                    _walkableNative = new NativeArray<byte>(length, Allocator.Persistent);
                }
            }
            else
            {
                _walkableNative = new NativeArray<byte>(length, Allocator.Persistent);
            }

            if (_dirtyHasRect && _walkableNative.Length == length)
            {
                CopyDirtyRectToNative();
            }
            else
            {
                CopyWholeGridToNative();
            }

            _nativeDirty = false;
            _dirtyHasRect = false;
        }

        private void MarkWalkableDirty()
        {
            _nativeDirty = true;
            _dirtyHasRect = false;
            _walkableVersion++;
        }

        private void MarkWalkableDirtyRect(int minCol, int minRow, int maxCol, int maxRow)
        {
            _nativeDirty = true;
            _walkableVersion++;
            if (!_dirtyHasRect)
            {
                _dirtyMinCol = minCol;
                _dirtyMaxCol = maxCol;
                _dirtyMinRow = minRow;
                _dirtyMaxRow = maxRow;
                _dirtyHasRect = true;
                return;
            }

            _dirtyMinCol = Mathf.Min(_dirtyMinCol, minCol);
            _dirtyMaxCol = Mathf.Max(_dirtyMaxCol, maxCol);
            _dirtyMinRow = Mathf.Min(_dirtyMinRow, minRow);
            _dirtyMaxRow = Mathf.Max(_dirtyMaxRow, maxRow);
        }

        // [HPFB-06]
        // Native resource cleanup and job-safe buffer synchronization helpers.
        private void OnDestroy()
        {
            var queue = PathRequestQueue.Instance;
            if (queue != null)
            {
                queue.CompleteActiveJobAndClear();
            }

            if (_walkableNative.IsCreated)
            {
                _walkableNative.Dispose();
            }
        }

        private float ResolveSampleRadius()
        {
            return SampleRadius > 0f ? SampleRadius : HexSize * 0.45f;
        }

        private int ResolveObstacleMaskValue()
        {
            var maskValue = ObstacleMask.value;
            if (maskValue != 0)
            {
                return maskValue;
            }

            var obstacleLayer = LayerMask.NameToLayer("Obstacles");
            return obstacleLayer >= 0 ? (1 << obstacleLayer) : ~0;
        }

        private void CopyDirtyRectToNative()
        {
            var c0 = Mathf.Clamp(_dirtyMinCol, 0, Width - 1);
            var c1 = Mathf.Clamp(_dirtyMaxCol, 0, Width - 1);
            var r0 = Mathf.Clamp(_dirtyMinRow, 0, Height - 1);
            var r1 = Mathf.Clamp(_dirtyMaxRow, 0, Height - 1);
            for (int row = r0; row <= r1; row++)
            {
                var baseIndex = row * Width;
                for (int col = c0; col <= c1; col++)
                {
                    _walkableNative[baseIndex + col] = (byte)(_walkable[row, col] ? 1 : 0);
                }
            }
        }

        private void CopyWholeGridToNative()
        {
            var index = 0;
            for (int row = 0; row < Height; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    _walkableNative[index++] = (byte)(_walkable[row, col] ? 1 : 0);
                }
            }
        }
    }
}
