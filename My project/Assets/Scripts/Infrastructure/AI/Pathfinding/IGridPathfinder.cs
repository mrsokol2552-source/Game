using System.Collections.Generic;

namespace Game.Infrastructure.AI.Pathfinding
{
    // Path point on integer grid
    public readonly struct GridPoint
    {
        public readonly int X;
        public readonly int Y;
        public GridPoint(int x, int y) { X = x; Y = y; }
    }

    public interface IGridPathfinder
    {
        // Fast feasibility check (no path returned)
        bool IsReachable(int fromX, int fromY, int toX, int toY);

        // A* path; returns true if found and fills 'path' from start->goal (inclusive).
        // Implementations should Clear() the list before filling.
        bool FindPath(int fromX, int fromY, int toX, int toY, List<GridPoint> path);
    }
}
