namespace RtsGame.Infrastructure.AI.Pathfinding
{
    public interface IGridPathfinder
    {
        // Returns true if a path likely exists between two grid cells; stub for now
        bool CanReach(int fromX, int fromY, int toX, int toY);
    }
}

