namespace CaveCrafter.CaveGen.Core
{
    /// <summary>
    /// Deterministic RNG interface used by cave generation.
    /// Lightweight and explicit for testability and repeatability.
    /// </summary>
    public interface IRng
    {
        /// <summary>Returns an int in range [minInclusive, maxInclusive).</summary>
        int NextInt(int minInclusive, int maxInclusive);

        /// <summary>Returns a float in range [0, 1).</summary>
        float NextFloat01();

        /// <summary>Returns true with probability p (0 - 1).</summary>
        bool Chance(float p);

        /// <summary>Returns either -1 or +1.</summary>
        int NextSign();
    }
}