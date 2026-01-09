using System.Collections.Generic;
using CaveCrafter.CaveGen.Core.Planning;

namespace CaveCrafter.CaveGen.Api
{
    /// <summary>
    /// Public result object for cave generation.
    /// </summary>
    public sealed class CaveGenResult
    {
        public IReadOnlyList<HighwayIntent> HighwayIntents { get; }

        public CaveGenResult(IReadOnlyList<HighwayIntent> highwayIntents)
        {
            HighwayIntents = highwayIntents;
        }
    }
}