namespace Match3.Core
{
    /// <summary>
    /// Configuration settings for Match3BoardModel.
    /// Flexible across 5x5, 7x7, 8x8 with customizable match lengths, color count, and hard safety limits.
    /// </summary>
    public sealed class Match3BoardConfig
    {
        public int Width { get; init; } = 7;
        public int Height { get; init; } = 7;
        public int MinimumMatchLength { get; init; } = 3;
        public int ColorCount { get; init; } = 5;
        public int MaxCascadeDepth { get; init; } = 32;
        public int MaxShuffleAttempts { get; init; } = 100;
        public int MaxGenerationAttempts { get; init; } = 100;
        public bool ThrowOnMaxCascadeDepthExceeded { get; init; } = false;

        public static Match3BoardConfig Default => new Match3BoardConfig();
    }
}
