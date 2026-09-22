using System;

namespace Match3.Core
{
    /// <summary>
    /// Lightweight value representation of a cell's tile state.
    /// Free of game-specific naming, subclasses, or engine references.
    /// Tracks occupancy explicitly so that TileColor.Red (0) is never confused with an empty cell.
    /// </summary>
    public readonly struct Match3Tile : IEquatable<Match3Tile>
    {
        public readonly TileColor Color;
        public readonly TileSpecial Special;
        public readonly bool IsOccupied;

        public bool IsEmpty => !IsOccupied;

        public static readonly Match3Tile Empty = default;

        public Match3Tile(TileColor color, TileSpecial special = TileSpecial.None)
        {
            Color = color;
            Special = special;
            IsOccupied = true;
        }

        public bool Equals(Match3Tile other) =>
            IsOccupied == other.IsOccupied && Color == other.Color && Special == other.Special;

        public override bool Equals(object obj) => obj is Match3Tile other && Equals(other);

        public override int GetHashCode() =>
            !IsOccupied ? 0 : (((int)Color * 397) ^ (int)Special);

        public override string ToString() =>
            !IsOccupied ? "Empty" : (Special == TileSpecial.None ? $"{Color}" : $"{Color}:{Special}");

        public static bool operator ==(Match3Tile left, Match3Tile right) => left.Equals(right);
        public static bool operator !=(Match3Tile left, Match3Tile right) => !left.Equals(right);
    }
}
