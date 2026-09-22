using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Match3.Core
{
    /// <summary>
    /// The complete, immutable result of a TrySwap operation.
    /// Deeply immutable: all step collections are wrapped in ReadOnlyCollection.
    /// </summary>
    public sealed class BoardResolution
    {
        public bool IsValidMove { get; }
        public BoardPosition From { get; }
        public BoardPosition To { get; }
        public IReadOnlyList<ResolutionStep> Steps { get; }
        public int CascadeCount { get; }
        public bool BoardAutoShuffled { get; }
        public bool MaxCascadeDepthExceeded { get; }

        public BoardResolution(
            bool isValidMove,
            BoardPosition from,
            BoardPosition to,
            IEnumerable<ResolutionStep> steps = null,
            int cascadeCount = 0,
            bool boardAutoShuffled = false,
            bool maxCascadeDepthExceeded = false)
        {
            IsValidMove = isValidMove;
            From = from;
            To = to;
            Steps = steps != null
                ? new ReadOnlyCollection<ResolutionStep>(new List<ResolutionStep>(steps))
                : (IReadOnlyList<ResolutionStep>)Array.Empty<ResolutionStep>();
            CascadeCount = cascadeCount;
            BoardAutoShuffled = boardAutoShuffled;
            MaxCascadeDepthExceeded = maxCascadeDepthExceeded;
        }

        public static BoardResolution Invalid(BoardPosition from, BoardPosition to) =>
            new BoardResolution(false, from, to);
    }
}
