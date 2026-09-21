/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Geometry.cs
@module: presentation.pathfinding.hexgrid
@purpose: Extracted hex/world conversion, gizmo drawing, and grid-geometry helpers for HexPathfindingBootstrap.
@entry: HPFB-05
@api: HexPathfindingBootstrap partial geometry helpers
@deps: Unity gizmos and camera projections
@data: grid/world conversion math and lightweight grid info snapshots
@perf: hot for coordinate conversion, cold for gizmo drawing
@thread: main thread only
@tests: manual grid/world alignment verification
@config: HexSize, Origin, DrawGrid, DrawOnlyVisible, GridColor
@assets: none
@notes: future render migration should keep these conversions stable because gameplay still depends on the hex grid
*/

using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP-GEOMETRY]
// Logical block: HexPathfindingBootstrap geometry/conversion extraction.

namespace Game.Presentation.Pathfinding
{
    public partial class HexPathfindingBootstrap
    {
        // [HPFB-05]
        // Hex-coordinate conversion helpers, gizmo drawing, and lightweight geometry snapshots.
        public Vector2Int WorldToGrid(Vector3 world)
        {
            var x = world.x - Origin.x;
            var y = world.y - Origin.y;
            var qf = (Mathf.Sqrt(3f) / 3f * x - 1f / 3f * y) / HexSize;
            var rf = (2f / 3f * y) / HexSize;
            var (q, r) = AxialRound(qf, rf);
            var col = q + (r - (r & 1)) / 2;
            var row = r;
            col = Mathf.Clamp(col, 0, Width - 1);
            row = Mathf.Clamp(row, 0, Height - 1);
            return new Vector2Int(col, row);
        }

        public Vector3 GridToWorld(int col, int row)
        {
            var q = col - (row - (row & 1)) / 2;
            var r = row;
            var worldX = HexSize * Mathf.Sqrt(3f) * (q + (r * 0.5f));
            var worldY = HexSize * (3f / 2f) * r;
            return new Vector3(Origin.x + worldX, Origin.y + worldY, 0f);
        }

        private static (int q, int r) AxialRound(float qf, float rf)
        {
            var xf = qf;
            var zf = rf;
            var yf = -xf - zf;
            var xi = Mathf.RoundToInt(xf);
            var yi = Mathf.RoundToInt(yf);
            var zi = Mathf.RoundToInt(zf);
            var dx = Mathf.Abs(xi - xf);
            var dy = Mathf.Abs(yi - yf);
            var dz = Mathf.Abs(zi - zf);
            if (dx > dy && dx > dz)
            {
                xi = -yi - zi;
            }
            else if (dy > dz)
            {
                yi = -xi - zi;
            }
            else
            {
                zi = -xi - yi;
            }

            return (xi, zi);
        }

        private float HexWidth => Mathf.Sqrt(3f) * HexSize;
        private float HexHeight => 2f * HexSize;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.6f, 0.9f);
            var width = HexWidth;
            var height = HexHeight * 0.75f;
            var center = new Vector3(Origin.x + (Width * width) * 0.5f, Origin.y + (Height * height) * 0.5f, 0f);
            Gizmos.DrawWireCube(center, new Vector3(Width * width, Height * height, 0f));
        }

        private void OnDrawGizmos()
        {
            if (!DrawGrid)
            {
                return;
            }

            var cam = Camera.current != null ? Camera.current : Camera.main;
            var worldRect = default(Rect);
            if (cam != null && cam.orthographic)
            {
                var width = cam.orthographicSize * cam.aspect * 2f;
                var height = cam.orthographicSize * 2f;
                worldRect = new Rect(cam.transform.position.x - width / 2f, cam.transform.position.y - height / 2f, width, height);
                var margin = HexHeight;
                worldRect.xMin -= margin;
                worldRect.xMax += margin;
                worldRect.yMin -= margin;
                worldRect.yMax += margin;
            }

            Gizmos.color = GridColor;
            for (int row = 0; row < Height; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    var center = GridToWorld(col, row);
                    if (DrawOnlyVisible && cam != null && cam.orthographic)
                    {
                        if (center.x < worldRect.xMin || center.x > worldRect.xMax || center.y < worldRect.yMin || center.y > worldRect.yMax)
                        {
                            continue;
                        }
                    }

                    DrawHex(center, HexSize);
                    if (_walkable != null && row < _walkable.GetLength(0) && col < _walkable.GetLength(1) && !_walkable[row, col])
                    {
                        Gizmos.color = Color.red;
                        var arm = HexSize * 0.6f;
                        Gizmos.DrawLine(center + new Vector3(-arm, -arm, 0f), center + new Vector3(arm, arm, 0f));
                        Gizmos.DrawLine(center + new Vector3(-arm, arm, 0f), center + new Vector3(arm, -arm, 0f));
                        Gizmos.color = GridColor;
                    }
                }
            }
        }

        private void DrawHex(Vector3 center, float size)
        {
            var previous = Vector3.zero;
            var first = Vector3.zero;
            for (int index = 0; index < 6; index++)
            {
                var angleDegrees = 60f * index - 30f;
                var radians = angleDegrees * Mathf.Deg2Rad;
                var point = new Vector3(center.x + size * Mathf.Cos(radians), center.y + size * Mathf.Sin(radians), 0f);
                if (index == 0)
                {
                    first = point;
                }
                else
                {
                    Gizmos.DrawLine(previous, point);
                }

                previous = point;
            }

            Gizmos.DrawLine(previous, first);
        }

        public struct GridInfo
        {
            public int Width;
            public int Height;
            public float HexSize;
            public Vector2 Origin;
        }

        public GridInfo GetGridInfo()
        {
            return new GridInfo
            {
                Width = Width,
                Height = Height,
                HexSize = HexSize,
                Origin = Origin
            };
        }
    }
}
