using System;

namespace Match3.Random
{
    /// <summary>
    /// Portable, deterministic implementation of PCG32 (Permuted Congruential Generator).
    /// Generates identical random streams across all .NET versions, Unity Mono, IL2CPP, and operating systems.
    /// Uses rejection sampling to eliminate modulo bias.
    /// Persistence contract identifier: "pcg32-v1".
    /// </summary>
    public sealed class Pcg32Match3Random : IMatch3Random
    {
        public const string AlgorithmId = "pcg32-v1";

        private ulong _state;
        private readonly ulong _inc;

        public string Algorithm => AlgorithmId;
        public ulong State => _state;

        public Pcg32Match3Random(ulong seed, ulong stream = 54UL)
        {
            _inc = (stream << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong oldState = _state;
            _state = unchecked(oldState * 6364136223846793005UL + _inc);
            uint xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rot = (int)(oldState >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
                throw new ArgumentException($"minInclusive ({minInclusive}) must be less than maxExclusive ({maxExclusive}).");

            uint range = (uint)(maxExclusive - minInclusive);
            // Rejection sampling for uniform distribution without modulo bias
            uint threshold = (uint)((0x100000000UL - range) % range);
            uint r;
            do
            {
                r = NextUInt();
            } while (r < threshold);

            return (int)(minInclusive + (r % range));
        }
    }
}
