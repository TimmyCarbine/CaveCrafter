using System;
using System.Runtime.CompilerServices;

namespace CaveCrafter.CaveGen.Core
{
    /// <summary>
    /// Split RNG: creates deterministic sub-streams for each generation phase.
    /// This prevents later tuning (i.e Links) from reshuffling earlier phases (e.g Highways).
    /// </summary>
    public sealed class SplitRng
    {
        private readonly int _rootSeed;

        public SplitRng(int rootSeed)
        {
            _rootSeed = rootSeed;
        }

        /// <summary>
        /// Creates a deterministic RNG stream for a specific phase.
        /// Optional salt allows the creation of multiple independent streams inside the same phase.
        /// </summary>
        public IRng ForPhase(int phaseId, int salt = 0)
        {
            int combinedSeed = HashSeed(_rootSeed, phaseId, salt);
            return new XorShift32Rng((uint)combinedSeed);
        }

        private static int HashSeed(int rootSeed, int phaseId, int salt)
        {
            // Simple, stable integer mixing (deterministic across platforms).
            // Avoids .NET HashCode randomness.
            unchecked
            {
                int h = 17;
                h = (h * 31) ^ rootSeed;
                h = (h * 31) ^ phaseId;
                h = (h * 31) ^ salt;

                // Final avalanche mix.
                h ^= (h >> 16);
                h = h * (int)0x7feb352d;
                h ^= (h >> 15);
                h = h * (int)0x846ca68b;
                h ^= (h >> 16);

                return h;
            }
        }

        /// <summary>
        /// Compact deterministic RNG (XorShift32).
        /// Goo enough for procedural generation and extremely fast.
        /// </summary>
        private sealed class XorShift32Rng : IRng
        {
            private uint _state;

            public XorShift32Rng(uint seed)
            {
                // Xorshift can;t have a zero state
                _state = seed == 0 ? 0x6D2B79F5u : seed;
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive)
                    throw new ArgumentException("maxExclusive must be > minInclusive");

                uint range = (uint)(maxExclusive - minInclusive);
                uint value = NextUInt();

                // Modulo bias is negligible for intended use.
                // Rejection sampling can be used later if perfect distibution is needed.
                uint result = value % range;
                return minInclusive + (int)result;
            }

            public float NextFloat01()
            {
                // 24bit mantissa float in range [0,1)
                uint v = NextUInt();
                return (v & 0x00FFFFFFu) / 16777216f;
            }

            public bool Chance(float p)
            {
                if (p <= 0f) return false;
                if (p >= 1f) return true;
                return NextFloat01() < p;
            }

            public int NextSign()
            {
                return Chance(0.5f) ? 1 : -1;
            }

            private uint NextUInt()
            {
                // Xorshift32
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }
        }
    }
}