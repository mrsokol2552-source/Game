using System;

namespace Match3.Core
{
    /// <summary>
    /// Discrete integer coordinates on the match-3 grid.
    /// 
    /// Canonical Coordinate Convention:
    /// - (0, 0) is Bottom-Left.
    /// - X increases to the Right (0 .. Width - 1).
    /// - Y increases Upwards (0 .. Height - 1).
    /// - Gravity: Tiles drop from higher Y (above) to lower Y (below/bottom) within a column.
    /// - Canonical Ordering: Y ascending, then X ascending.
    /// </summary>
    public readonly struct BoardPosition : IEquatable<BoardPosition>, IComparable<BoardPosition>
    {
        public readonly int X;
        public readonly int Y;

        public BoardPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(BoardPosition other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is BoardPosition other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(BoardPosition left, BoardPosition right) => left.Equals(right);
        public static bool operator !=(BoardPosition left, BoardPosition right) => !left.Equals(right);

        /// <summary>
        /// Canonical ordering: Y ascending, then X ascending.
        /// Guarantees byte-equivalent ordering across all platforms and collections.
        /// </summary>
        public int CompareTo(BoardPosition other)
        {
            int cmp = Y.CompareTo(other.Y);
            if (cmp != 0) return cmp;
            return X.CompareTo(other.X);
        }

        /// <summary>
        /// Checks if two positions are orthogonal neighbors (distance = 1, non-diagonal).
        /// </summary>
        public bool IsOrthogonalNeighbor(BoardPosition other)
        {
            int dx = Math.Abs(X - other.X);
            int dy = Math.Abs(Y - other.Y);
            return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
        }
    }
}
