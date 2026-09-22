namespace Match3.Random
{
    /// <summary>
    /// Seeded deterministic random number generator backed by PCG32.
    /// Provides consistent, cross-runtime reproducibility for match-3 board generation, replays, and telemetry.
    /// </summary>
    public sealed class SeededMatch3Random : IMatch3Random
    {
        private readonly Pcg32Match3Random _inner;

        public string Algorithm => _inner.Algorithm;
        public ulong State => _inner.State;

        public SeededMatch3Random(int seed)
        {
            _inner = new Pcg32Match3Random((ulong)(uint)seed);
        }

        public SeededMatch3Random(ulong seed, ulong stream = 54UL)
        {
            _inner = new Pcg32Match3Random(seed, stream);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            return _inner.Next(minInclusive, maxExclusive);
        }
    }
}
