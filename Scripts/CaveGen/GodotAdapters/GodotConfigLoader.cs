using CaveCrafter.CaveGen.Config;

namespace CaveCrafter.CaveGen.GodotAdapters
{
    /// <summary>
    /// Converts Godot Resource config assets into pure C# DTOs.
    /// Keeps Godot types out of the core generation layer.
    /// </summary>
    public static class GodotConfigLoader
    {
        public static CaveGenConfig Load(CaveGenConfigResource resource)
        {
            TunnelClassConfig highway = LoadTunnel(resource.Highway);
            return new CaveGenConfig(highway);
        }

        private static TunnelClassConfig LoadTunnel(TunnelClassConfigResource r)
        {
            return new TunnelClassConfig(
                startYMinFrac: r.StartYMinFrac,
                startYMaxFrac: r.StartYMaxFrac,
                endYMinFrac: r.EndYMinFrac,
                endYMaxFrac: r.EndYMaxFrac,
                countPerWidthTiles: r.CountPerWidthTiles,
                minCount: r.MinCount,
                maxCount: r.MaxCount,
                countVariance: r.CountVariance,
                minDiagonalDyOverDx: r.MinDiagonalDyOverDx,
                minAngleDeg: r.MinAngleDeg,
                maxAngleDeg: r.MaxAngleDeg,
                minDownwardDeltaYFracMin: r.MinDownwardDeltaYFracMin,
                minDownwardDeltaYFracMax: r.MinDownwardDeltaYFracMax,
                edgeBiasFrac: r.EdgeBiasFrac
            );
        }
    }
}