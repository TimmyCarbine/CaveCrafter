using System;
using CaveCrafter.CaveGen.Config;
using CaveCrafter.CaveGen.Core;
using CaveCrafter.CaveGen.Core.Planning;
using CaveCrafter.CaveGen.GodotAdapters;

namespace CaveCrafter.CaveGen.Api
{
    /// <summary>
    /// Orchestrates the cave generation pipeline.
    /// </summary>
    public static class CaveGenerator
    {
        public static CaveGenResult GeneratePhase1(CaveGenRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.ConfigResource == null) throw new ArgumentNullException(nameof(request.ConfigResource));

            // 1) Load config (Resource -> pure DTO)
            CaveGenConfig config = GodotConfigLoader.Load(request.ConfigResource);

            // 2) Scale config to map size (fractions -> tile values)
            ScaledCaveGenConfig scaled = CaveConfigScaler.Scale(config, request.MapWidth, request.MapHeight);

            // 3) Deterministic RNG stream for this phase
            SplitRng split = new SplitRng(request.Seed);
            IRng rng = split.ForPhase((int)CaveGenPahse.HighwayAnchors);

            // 4) Plan
            HighwayPlanner planner = new HighwayPlanner();
            var highwayIntents = planner.Plan(request.MapWidth, request.MapHeight, scaled.Highway, rng);

            return new CaveGenResult(highwayIntents);
        }
    }
}