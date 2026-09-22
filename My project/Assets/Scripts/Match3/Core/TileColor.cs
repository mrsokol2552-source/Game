namespace Match3.Core
{
    /// <summary>
    /// Five persistent color families for tokens.
    /// Semantics are interpreted by external layers (e.g. CombatBridge, ColonyManager),
    /// while Match3BoardModel treats them purely as discrete color identifiers.
    /// None (255) represents non-color-aligned events (e.g. board-wide combos).
    /// </summary>
    public enum TileColor : byte
    {
        Red = 0,
        Blue = 1,
        Yellow = 2,
        Green = 3,
        Purple = 4,
        None = 255
    }
}
