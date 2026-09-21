/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.cs
@module: presentation.pathfinding.hexgrid
@purpose: Owns the authoritative odd-r hex grid, walkability state, Native mirrors, and grid/world conversion helpers.
@entry: HexPathfindingBootstrap.Awake, HPFB-02, HPFB-03
@api: scene MonoBehaviour singleton backing all hex pathfinding systems
@deps: grid pathfinder implementation, ProceduralEnvironment, Physics2D collider bake
@data: walkable grid, dirty rectangles, NativeArray mirrors, grid dimensions
@perf: hotpath for coordinate conversion and walkability queries; memory-sensitive at large map sizes
@thread: main thread with Native data exposed to jobs
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual map generation verification
@config: Width, Height, MaxCells, AutoClampSize, docs/runtime_switches.md
@assets: collider layers and obstacle bake inputs
@notes: if grid dimensions and world generation size drift apart, streaming, baking, and prop placement will all misbehave
*/

using Game.Infrastructure.AI.Pathfinding;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP]
// Logical block: Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.

namespace Game.Presentation.Pathfinding
{
    // Hex grid bootstrap: odd-r offset storage, pointy-top hexes.
    public partial class HexPathfindingBootstrap : MonoBehaviour
    {
        // [HPFB-01]
        // Hex-grid dimensions, walkability storage, and NativeArray mirrors used by jobs/pathfinding.
        [Header("Hex Grid Settings (Odd-R)")]
        public int Width = 1024;   // columns (q/col)
        public int Height = 1024;  // rows (r)
        public float HexSize = 0.4f; // radius of hex (center->corner)
        public Vector2 Origin = Vector2.zero;
        public bool AutoFitToCamera = true;
        [Header("Grid Safety")]
        [Tooltip("Clamp grid size if Width*Height exceeds this value (prevents huge allocations).")]
        public bool AutoClampSize = true;
        public int MaxCells = 1_500_000;
        [Header("Obstacles (Bake)")]
        public bool AutoBakeColliders = false;
        public LayerMask ObstacleMask;
        [Tooltip("Sampling radius around hex center for collider check (defaults to HexSize*0.45)")]
        public float SampleRadius = -1f;
        [Header("Debug Gizmos")]
        public bool DrawGrid = true;
        public bool DrawOnlyVisible = true;
        public Color GridColor = new Color(0.3f, 0.9f, 0.6f, 0.8f);
        [Tooltip("Log blocked cells count after bake")] public bool LogBake = true;

        private IGridPathfinder _pathfinder;
        private bool[,] _walkable;
        private Unity.Collections.NativeArray<byte> _walkableNative;
        private bool _nativeDirty = true;
        private bool _dirtyHasRect;
        private int _dirtyMinCol;
        private int _dirtyMaxCol;
        private int _dirtyMinRow;
        private int _dirtyMaxRow;
        private int _walkableVersion = 1;

        // [HPFB-02]
        // Initial grid sizing and one-time bootstrap initialization.
        private void Awake()
        {
            InitializeGrid();
        }

        // [HPFB-03]
        // Lazy initialization and reallocation guards for managed/native walkability buffers.
        public void EnsureInitialized()
        {
            if (_walkable != null) return;
            InitializeGrid();
        }

        private void InitializeGrid()
        {
            if (AutoClampSize && Width > 0 && Height > 0)
            {
                long cells = (long)Width * (long)Height;
                if (cells > MaxCells && MaxCells > 0)
                {
                    float scale = Mathf.Sqrt((float)MaxCells / (float)cells);
                    int newW = Mathf.Max(4, Mathf.RoundToInt(Width * scale));
                    int newH = Mathf.Max(4, Mathf.RoundToInt(Height * scale));
                    Debug.LogWarning($"[HexPathfinding] Grid clamped from {Width}x{Height} ({cells} cells) to {newW}x{newH} (<= {MaxCells}). Adjust MaxCells or disable AutoClampSize if needed.");
                    Width = newW; Height = newH;
                }
            }
            _walkable = new bool[Height, Width];
            for (int r = 0; r < Height; r++) for (int q = 0; q < Width; q++) _walkable[r, q] = true;
            if (AutoFitToCamera) FitToCamera();
            _pathfinder = HexPathfinder.FromWalkableMap(_walkable);
            if (AutoBakeColliders) BakeFromPhysics();
            UpdateNativeWalkable();
        }

        public IGridPathfinder Pathfinder => _pathfinder;
        public int WalkableVersion => _walkableVersion;

        public bool IsWalkable(int col, int row)
        {
            if (_walkable == null) return true;
            if (col < 0 || row < 0 || col >= Width || row >= Height) return false;
            int h = _walkable.GetLength(0);
            int w = _walkable.GetLength(1);
            if (row < 0 || row >= h || col < 0 || col >= w) return false;
            return _walkable[row, col];
        }

        public bool IsWalkableWorld(Vector3 world)
        {
            var cell = WorldToGrid(world);
            return IsWalkable(cell.x, cell.y);
        }

    }
}
