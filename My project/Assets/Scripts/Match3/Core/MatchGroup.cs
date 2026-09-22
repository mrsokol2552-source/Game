using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Match3.Core
{
    /// <summary>
    /// Geometric shape classification of a resolved match group.
    /// Precedence for power-up creation:
    /// Intersection (Dynamite) > Line5Plus (Airstrike) > Line4 (Rocket) > Line3 (None).
    /// </summary>
    public enum MatchShape : byte
    {
        Line3 = 0,
        Line4 = 1,
        Line5Plus = 2,
        Intersection = 3 // T-shape, L-shape, or Cross
    }

    /// <summary>
    /// Represents an immutable contiguous group of matched tiles of the same color.
    /// Crossing or intersecting runs are merged into a single MatchGroup.
    /// Positions are always stored in canonical order (Y ascending, then X ascending).
    /// </summary>
    public sealed class MatchGroup
    {
        public TileColor Color { get; }
        public IReadOnlyList<BoardPosition> Positions { get; }
        public MatchShape Shape { get; }
        public bool IsHorizontal { get; }
        public BoardPosition? IntersectionPoint { get; }

        public MatchGroup(
            TileColor color,
            IEnumerable<BoardPosition> positions,
            MatchShape shape = MatchShape.Line3,
            bool isHorizontal = true,
            BoardPosition? intersectionPoint = null)
        {
            Color = color;
            var list = positions != null ? new List<BoardPosition>(positions) : new List<BoardPosition>();
            list.Sort();
            Positions = new ReadOnlyCollection<BoardPosition>(list);
            Shape = shape;
            IsHorizontal = isHorizontal;
            IntersectionPoint = intersectionPoint;
        }
    }
}
