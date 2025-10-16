namespace Game.Infrastructure.AI.Pathfinding
{
    public interface IGridPathfinder
    {
        bool IsReachable(int fromX, int fromY, int toX, int toY);
        // Placeholder; actual path result can be added later.
    }
}

