using System.Collections.Generic;
using Game.Infrastructure.AI.Pathfinding;
using UnityEngine;

namespace Game.Presentation.Pathfinding
{
    public class PathfindingBootstrap : MonoBehaviour
    {
        [Header("Grid Settings")]
        public int Width = 64;
        public int Height = 64;
        public float CellSize = 1f;
        public Vector2 Origin = Vector2.zero;
        [Header("Options")]
        public bool AllowDiagonals = true;
        public bool AutoFitToCamera = true;
        public bool SmoothWorldPath = true;
        [Header("Debug Gizmos")]
        public bool DrawGrid = true;
        public bool DrawOnlyVisible = true;
        public Color GridColor = new Color(0f, 0.8f, 1f, 0.5f);

        private IGridPathfinder _pathfinder;

        private bool[,] _walkable;

        private void Awake()
        {
            _walkable = new bool[Height, Width];
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) _walkable[y, x] = true; // all walkable by default
            if (AutoFitToCamera) FitToCamera();
            _pathfinder = GridPathfinder.FromWalkableMap(_walkable, AllowDiagonals);
        }

        public IGridPathfinder Pathfinder => _pathfinder;

        public Vector2Int WorldToGrid(Vector3 world)
        {
            float lx = world.x - Origin.x;
            float ly = world.y - Origin.y;
            int gx = Mathf.FloorToInt(lx / Mathf.Max(0.0001f, CellSize));
            int gy = Mathf.FloorToInt(ly / Mathf.Max(0.0001f, CellSize));
            gx = Mathf.Clamp(gx, 0, Width - 1);
            gy = Mathf.Clamp(gy, 0, Height - 1);
            return new Vector2Int(gx, gy);
        }

        public Vector3 GridToWorld(int gx, int gy)
        {
            float x = Origin.x + (gx + 0.5f) * CellSize;
            float y = Origin.y + (gy + 0.5f) * CellSize;
            return new Vector3(x, y, 0f);
        }

        // Future extension: expose methods to mark cells blocked/unblocked by buildings.
        public void SetWalkable(int gx, int gy, bool walkable)
        {
            if (gx < 0 || gy < 0 || gx >= Width || gy >= Height) return;
            _walkable[gy, gx] = walkable;
        }

        public void SetAllowDiagonals(bool allow)
        {
            if (AllowDiagonals == allow) return;
            AllowDiagonals = allow;
            _pathfinder = GridPathfinder.FromWalkableMap(_walkable, AllowDiagonals);
        }

        public void FitToCamera()
        {
            var cam = Camera.main; if (cam == null) return;
            // Center grid on camera so viewport попадает внутрь сетки
            var center = cam.transform.position;
            float w = Width * CellSize;
            float h = Height * CellSize;
            Origin = new Vector2(center.x - w * 0.5f, center.y - h * 0.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
            float w = Width * CellSize;
            float h = Height * CellSize;
            var center = new Vector3(Origin.x + w * 0.5f, Origin.y + h * 0.5f, 0f);
            Gizmos.DrawWireCube(center, new Vector3(w, h, 0f));
        }

        private void OnDrawGizmos()
        {
            if (!DrawGrid) return;
            var cam = Camera.current != null ? Camera.current : Camera.main;
            Rect worldRect = default;
            if (cam != null && cam.orthographic)
            {
                float w = cam.orthographicSize * cam.aspect * 2f;
                float h = cam.orthographicSize * 2f;
                worldRect = new Rect(cam.transform.position.x - w / 2f, cam.transform.position.y - h / 2f, w, h);
                worldRect.xMin -= CellSize * 2f; worldRect.xMax += CellSize * 2f;
                worldRect.yMin -= CellSize * 2f; worldRect.yMax += CellSize * 2f;
            }

            Gizmos.color = GridColor;
            Vector3 size = new Vector3(CellSize, CellSize, 0f);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var cx = Origin.x + (x + 0.5f) * CellSize;
                    var cy = Origin.y + (y + 0.5f) * CellSize;
                    if (DrawOnlyVisible && cam != null && cam.orthographic)
                    {
                        if (cx < worldRect.xMin || cx > worldRect.xMax || cy < worldRect.yMin || cy > worldRect.yMax)
                            continue;
                    }
                    var center = new Vector3(cx, cy, 0f);
                    Gizmos.DrawWireCube(center, size);
                    // Mark blocked cells (if any) with a red cross
                    if (_walkable != null && y < _walkable.GetLength(0) && x < _walkable.GetLength(1) && !_walkable[y, x])
                    {
                        var half = CellSize * 0.5f;
                        Gizmos.color = Color.red;
                        Gizmos.DrawLine(center + new Vector3(-half, -half, 0f), center + new Vector3(half, half, 0f));
                        Gizmos.DrawLine(center + new Vector3(-half, half, 0f), center + new Vector3(half, -half, 0f));
                        Gizmos.color = GridColor;
                    }
                }
            }
        }
    }
}
