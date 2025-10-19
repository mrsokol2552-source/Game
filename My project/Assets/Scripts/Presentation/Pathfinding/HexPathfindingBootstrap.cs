using Game.Infrastructure.AI.Pathfinding;
using UnityEngine;

namespace Game.Presentation.Pathfinding
{
    // Hex grid bootstrap: odd-r offset storage, pointy-top hexes.
    public class HexPathfindingBootstrap : MonoBehaviour
    {
        [Header("Hex Grid Settings (Odd-R)")]
        public int Width = 64;   // columns (q/col)
        public int Height = 64;  // rows (r)
        public float HexSize = 1f; // radius of hex (center->corner)
        public Vector2 Origin = Vector2.zero;
        public bool AutoFitToCamera = true;
        [Header("Obstacles (Bake)")]
        public bool AutoBakeColliders = true;
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

        private void Awake()
        {
            _walkable = new bool[Height, Width];
            for (int r = 0; r < Height; r++) for (int q = 0; q < Width; q++) _walkable[r, q] = true;
            if (AutoFitToCamera) FitToCamera();
            _pathfinder = HexPathfinder.FromWalkableMap(_walkable);
            if (AutoBakeColliders) BakeFromPhysics();
        }

        public IGridPathfinder Pathfinder => _pathfinder;

        public Vector2Int WorldToGrid(Vector3 world)
        {
            // Convert from world (relative to Origin) to axial (q,r), then to odd-r offset (col,row)
            float x = world.x - Origin.x;
            float y = world.y - Origin.y;
            float qf = (Mathf.Sqrt(3f) / 3f * x - 1f / 3f * y) / HexSize;
            float rf = (2f / 3f * y) / HexSize;
            var (q, r) = AxialRound(qf, rf);
            int col = q + (r - (r & 1)) / 2;
            int row = r;
            col = Mathf.Clamp(col, 0, Width - 1);
            row = Mathf.Clamp(row, 0, Height - 1);
            return new Vector2Int(col, row);
        }

        public Vector3 GridToWorld(int col, int row)
        {
            // odd-r to axial
            int q = col - (row - (row & 1)) / 2;
            int r = row;
            // axial to world (pointy-top)
            float wx = HexSize * Mathf.Sqrt(3f) * (q + (r * 0.5f));
            float wy = HexSize * (3f / 2f) * r;
            return new Vector3(Origin.x + wx, Origin.y + wy, 0f);
        }

        public void SetBlockedAtWorld(Vector3 world, bool blocked)
        {
            var cell = WorldToGrid(world);
            SetWalkable(cell.x, cell.y, !blocked);
        }

        public void ClearAllBlocks()
        {
            if (_walkable == null) return;
            for (int r = 0; r < Height; r++)
                for (int q = 0; q < Width; q++)
                    _walkable[r, q] = true;
        }

        public void BakeFromPhysics()
        {
            if (_walkable == null) return;
            float radius = SampleRadius > 0f ? SampleRadius : (HexSize * 0.45f);
            int maskVal = ObstacleMask.value;
            if (maskVal == 0)
            {
                // Fallback: try layer named "Obstacles"; if not found, scan all layers
                int obst = LayerMask.NameToLayer("Obstacles");
                maskVal = obst >= 0 ? (1 << obst) : ~0;
            }
            int blocked = 0;
            for (int r = 0; r < Height; r++)
            {
                for (int q = 0; q < Width; q++)
                {
                    var w = GridToWorld(q, r);
                    var hit = Physics2D.OverlapCircle(w, radius, maskVal);
                    bool walk = (hit == null);
                    _walkable[r, q] = walk;
                    if (!walk) blocked++;
                }
            }
            if (LogBake)
                Debug.Log($"[HexPathfinding] BakeFromPhysics: blocked={blocked} / total={Width*Height}, mask=0x{maskVal:X}");
        }

        [ContextMenu("Rebake Obstacles")]
        private void RebakeObstaclesInspector()
        {
            BakeFromPhysics();
        }

        public System.Collections.Generic.IEnumerable<Vector2Int> CaptureBlocked()
        {
            for (int r = 0; r < Height; r++)
                for (int q = 0; q < Width; q++)
                    if (_walkable != null && !_walkable[r, q]) yield return new Vector2Int(q, r);
        }

        public void RestoreBlocked(System.Collections.Generic.IEnumerable<Vector2Int> blocks)
        {
            ClearAllBlocks();
            if (blocks == null) return;
            foreach (var c in blocks)
            {
                if (c.y >= 0 && c.y < Height && c.x >= 0 && c.x < Width)
                    _walkable[c.y, c.x] = false;
            }
        }

        public void FitToCamera()
        {
            var cam = Camera.main; if (cam == null) return;
            float w = HexWidth * Width * 0.75f; // approximate envelope
            float h = HexHeight * Height * 0.5f;
            var center = cam.transform.position;
            Origin = new Vector2(center.x - w * 0.5f, center.y - h * 0.5f);
        }

        public void SetWalkable(int col, int row, bool walkable)
        {
            if (col < 0 || row < 0 || col >= Width || row >= Height) return;
            _walkable[row, col] = walkable;
        }

        private static (int q, int r) AxialRound(float qf, float rf)
        {
            // cube rounding
            float xf = qf;
            float zf = rf;
            float yf = -xf - zf;
            int xi = Mathf.RoundToInt(xf);
            int yi = Mathf.RoundToInt(yf);
            int zi = Mathf.RoundToInt(zf);
            float dx = Mathf.Abs(xi - xf);
            float dy = Mathf.Abs(yi - yf);
            float dz = Mathf.Abs(zi - zf);
            if (dx > dy && dx > dz) xi = -yi - zi; else if (dy > dz) yi = -xi - zi; else zi = -xi - yi;
            return (xi, zi);
        }

        private float HexWidth => Mathf.Sqrt(3f) * HexSize;
        private float HexHeight => 2f * HexSize;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.6f, 0.9f);
            float w = HexWidth; float h = HexHeight * 0.75f; // row spacing
            var center = new Vector3(Origin.x + (Width * w) * 0.5f, Origin.y + (Height * (HexHeight * 0.75f)) * 0.5f, 0f);
            Gizmos.DrawWireCube(center, new Vector3(Width * w, Height * (HexHeight * 0.75f), 0f));
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
                float m = HexHeight;
                worldRect.xMin -= m; worldRect.xMax += m; worldRect.yMin -= m; worldRect.yMax += m;
            }

            Gizmos.color = GridColor;
            for (int r = 0; r < Height; r++)
            {
                for (int q = 0; q < Width; q++)
                {
                    var c = GridToWorld(q, r);
                    if (DrawOnlyVisible && cam != null && cam.orthographic)
                    {
                        if (c.x < worldRect.xMin || c.x > worldRect.xMax || c.y < worldRect.yMin || c.y > worldRect.yMax)
                            continue;
                    }
                    DrawHex(c, HexSize);
                    // If blocked, draw a red X
                    if (_walkable != null && r < _walkable.GetLength(0) && q < _walkable.GetLength(1) && !_walkable[r, q])
                    {
                        Gizmos.color = Color.red;
                        float a = HexSize * 0.6f;
                        Gizmos.DrawLine(c + new Vector3(-a, -a, 0f), c + new Vector3(a, a, 0f));
                        Gizmos.DrawLine(c + new Vector3(-a, a, 0f), c + new Vector3(a, -a, 0f));
                        Gizmos.color = GridColor;
                    }
                }
            }
        }

        private void DrawHex(Vector3 center, float size)
        {
            // pointy-top hex corners
            Vector3 prev = Vector3.zero;
            Vector3 first = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                float angleDeg = 60f * i - 30f;
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector3 p = new Vector3(center.x + size * Mathf.Cos(rad), center.y + size * Mathf.Sin(rad), 0f);
                if (i == 0) { first = p; }
                else { Gizmos.DrawLine(prev, p); }
                prev = p;
            }
            Gizmos.DrawLine(prev, first);
        }
    }
}

