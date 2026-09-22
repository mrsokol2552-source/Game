using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Match3.Core
{
    /// <summary>
    /// Why a tile was cleared from the board.
    /// Invaluable for CombatBridge (calculating damage/ammo coefficients), presentation view, and analytics.
    /// </summary>
    public enum ClearCause : byte
    {
        NormalMatch = 0,
        Rocket = 1,
        Dynamite = 2,
        Airstrike = 3,
        SpecialCombo = 4
    }

    /// <summary>
    /// Rich snapshot of a tile removed from the grid.
    /// Preserves position, previous tile state (color and special), and clear cause.
    /// Ordered canonically by Position (Y ascending, then X ascending).
    /// </summary>
    public readonly struct ClearedTile : IEquatable<ClearedTile>, IComparable<ClearedTile>
    {
        public readonly BoardPosition Position;
        public readonly Match3Tile Tile;
        public readonly ClearCause Cause;

        public ClearedTile(BoardPosition position, Match3Tile tile, ClearCause cause)
        {
            Position = position;
            Tile = tile;
            Cause = cause;
        }

        public bool Equals(ClearedTile other) =>
            Position == other.Position && Tile == other.Tile && Cause == other.Cause;

        public override bool Equals(object obj) => obj is ClearedTile other && Equals(other);

        public override int GetHashCode() =>
            (Position.GetHashCode() * 397) ^ (Tile.GetHashCode() * 31) ^ (int)Cause;

        public int CompareTo(ClearedTile other) => Position.CompareTo(other.Position);

        public override string ToString() => $"{Position}: {Tile} by {Cause}";
    }

    /// <summary>
    /// Describes a special power-up detonation event.
    /// </summary>
    public readonly struct SpecialActivation
    {
        public readonly BoardPosition Origin;
        public readonly TileSpecial Special;
        public readonly TileColor TargetColor;
        public readonly IReadOnlyList<BoardPosition> ClearedPositions;

        public SpecialActivation(BoardPosition origin, TileSpecial special, TileColor targetColor, IEnumerable<BoardPosition> clearedPositions)
        {
            Origin = origin;
            Special = special;
            TargetColor = targetColor;
            ClearedPositions = clearedPositions != null
                ? new ReadOnlyCollection<BoardPosition>(new List<BoardPosition>(clearedPositions))
                : (IReadOnlyList<BoardPosition>)Array.Empty<BoardPosition>();
        }

        public override string ToString() => $"{Special} at {Origin} (Target: {TargetColor})";
    }

    /// <summary>
    /// Describes a tile shifting positions during swap or gravity compaction.
    /// Used by Match3BoardView to animate drops without performing any game math.
    /// </summary>
    public readonly struct TileMove : IComparable<TileMove>
    {
        public readonly BoardPosition From;
        public readonly BoardPosition To;

        public TileMove(BoardPosition from, BoardPosition to)
        {
            From = from;
            To = to;
        }

        public int CompareTo(TileMove other)
        {
            int cmp = To.CompareTo(other.To);
            if (cmp != 0) return cmp;
            return From.CompareTo(other.From);
        }

        public override string ToString() => $"{From} -> {To}";
    }

    /// <summary>
    /// Describes a newly generated tile entering from the top to refill the column.
    /// </summary>
    public readonly struct TileSpawn : IComparable<TileSpawn>
    {
        public readonly BoardPosition Position;
        public readonly Match3Tile Tile;

        public TileSpawn(BoardPosition position, Match3Tile tile)
        {
            Position = position;
            Tile = tile;
        }

        public int CompareTo(TileSpawn other) => Position.CompareTo(other.Position);

        public override string ToString() => $"{Position}: {Tile}";
    }

    /// <summary>
    /// Describes a special power-up created from combo matches.
    /// </summary>
    public readonly struct SpecialSpawn : IComparable<SpecialSpawn>
    {
        public readonly BoardPosition Position;
        public readonly TileSpecial Special;
        public readonly TileColor Color;

        public SpecialSpawn(BoardPosition position, TileSpecial special, TileColor color)
        {
            Position = position;
            Special = special;
            Color = color;
        }

        public int CompareTo(SpecialSpawn other) => Position.CompareTo(other.Position);

        public override string ToString() => $"{Special} ({Color}) at {Position}";
    }
}
