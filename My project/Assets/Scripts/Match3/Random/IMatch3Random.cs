namespace Match3.Random
{
    /// <summary>
    /// Pure deterministic pseudo-random number generator interface for the Match-3 model.
    /// Completely decouples the board model from UnityEngine.Random and System.Random.
    /// </summary>
    public interface IMatch3Random
    {
        /// <summary>
        /// Explicit persistence and replay identifier (e.g. "pcg32-v1").
        /// Guarantees cross-runtime replay capability across game versions.
        /// </summary>
        string Algorithm { get; }

        /// <summary>
        /// Returns a random integer within [minInclusive, maxExclusive).
        /// </summary>
        int Next(int minInclusive, int maxExclusive);

        /// <summary>
        /// Current internal generator state (useful for golden vector verification and mutation checks).
        /// </summary>
        ulong State { get; }
    }
}
