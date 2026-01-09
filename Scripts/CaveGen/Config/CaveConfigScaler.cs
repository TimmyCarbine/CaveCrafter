using CaveCrafter.CaveGen.Core;

namespace CaveCrafter.CaveGen.Config
{
    /// <summary>
    /// Converts fraction-based configs into tile-space configs for a specific map size.
    /// All size scaling lives here so the rest of the system stays clean.
    /// </summary>
    public static class CaveConfigScaler
    {
        public static ScaledCaveGenConfig Scale(CaveGenConfig config, int mapWidth, int mapHeight)
        {
            ScaledTunnelClassConfig highway = ScaleTunnelClass(config.Highway, mapHeight);
            return new ScaledCaveGenConfig(highway);
        }

        private static ScaledTunnelClassConfig ScaleTunnelClass(TunnelClassConfig cfg, int mapHeight)
        {
            // Convert bands from fractions to inclusive tile ranges.
            int startMin = FracToTileY(cfg.StartYMinFrac, mapHeight);
            int startMax = FracToTileY(cfg.StartYMaxFrac, mapHeight);

            int endMin = FracToTileY(cfg.EndYMinFrac, mapHeight);
            int endMax = FracToTileY(cfg.EndYMaxFrac, mapHeight);

            // Convert downward delta fraction ranges into tile ranges.
            int downMin = CoreMath.Clamp(CoreMath.RoundToInt(cfg.MinDownwardDeltaYFracMin * mapHeight), 0, mapHeight);
            int downMax = CoreMath.Clamp(CoreMath.RoundToInt(cfg.MinDownwardDeltaYFracMax * mapHeight), 0, mapHeight);

            // Sanitise ranges (protect against misconfigured assets).
            if (startMax < startMin) (startMin, startMax) = (startMax, startMin);
            if (endMax < endMin) (endMin, endMax) = (endMax, endMin);
            if (downMax < downMin) (downMin, downMax) = (downMax, downMin);

            // Clamp within tile bounds.
            startMin = CoreMath.Clamp(startMin, 0, mapHeight - 1);
            startMax = CoreMath.Clamp(startMax, 0, mapHeight - 1);
            endMin = CoreMath.Clamp(endMin, 0, mapHeight - 1);
            endMax = CoreMath.Clamp(endMax, 0, mapHeight - 1);

            // Ensure max is at least min (after clamping).
            startMax = startMax < startMin ? startMin : startMax;
            endMax = endMax < endMin ? endMin : endMax;

            return new ScaledTunnelClassConfig(
                startYMin: startMin,
                startYMax: startMax,
                endYMin: endMin,
                endYMax: endMax,
                countPerWidthTiles: cfg.CountPerWidthTiles,
                minCount: cfg.MinCount,
                maxCount: cfg.MaxCount,
                countVariance: cfg.CountVariance,
                minDiagonalDyOverDx: cfg.MinDiagonalDyOverDx,
                minAngleDeg: cfg.MinAngleDeg,
                maxAngleDeg: cfg.MaxAngleDeg,
                minDownwardDeltaYMin: downMin,
                minDownwardDeltaYMax: downMax,
                edgeBiasFrac: cfg.EdgeBiasFrac
            );
        }

        private static int FracToTileY(float frac, int mapHeight)
        {
            // frac=0 => y=0, frac=1 => y=mapHeight-1 (clamped)
            int y = CoreMath.RoundToInt(frac * (mapHeight - 1));
            return y;
        }
    }
}