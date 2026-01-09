using CaveCrafter.CaveGen.Config;

namespace CaveCrafter.CaveGen.Api
{
    /// <summary>
    /// Public request object for cave generation.
    /// This is the only thing the game layer needs to construct to run the generator.
    /// </summary>
    public sealed class CaveGenRequest
    {
        /// <summary>Map width in tiles.</summary>
        public int MapWidth { get; }

        /// <summary>Map height in tiles.</summary>
        public int MapHeight { get; }

        /// <summary>Root seed for deterministic generation.</summary>
        public int Seed { get; }

        /// <summary>
        /// Editor facing config asset. Convert this into a pure DTO before core generation.
        /// </summary>
        public CaveGenConfigResource ConfigResource { get; }

        public CaveGenRequest(int mapWidth, int mapHeight, int seed, CaveGenConfigResource configResource)
        {
            MapWidth = mapWidth;
            MapHeight = mapHeight;
            Seed = seed;
            ConfigResource = configResource;
        }
    }
}