namespace CaveCrafter.CaveGen.Config
{
    /// <summary>
    /// Map-size-scaled configuration (fractions converted into tile-space ranges).
    /// This is what planners actually use.
    /// </summary>
    public sealed class ScaledCaveGenConfig
    {
        public ScaledTunnelClassConfig Highway { get; }

        public ScaledCaveGenConfig(ScaledTunnelClassConfig highway)
        {
            Highway = highway;
        }
    }

    /// <summary>
    /// Tile-space tunnel class config.
    /// </summary>
    public sealed class ScaledTunnelClassConfig
    {
        public int StartYMin { get; }
        public int StartYMax { get; }

        public int EndYMin { get; }
        public int EndYMax { get; }

        public int CountPerWidthTiles { get; }
        public int MinCount { get; }
        public int MaxCount { get; }
        public int CountVariance { get; }

        public float MinDiagonalDyOverDx { get; }

        public float MinAngleDeg { get; }
        public float MaxAngleDeg { get; }

        public int MinDownwardDeltaYMin { get; }
        public int MinDownwardDeltaYMax { get; }

        public float EdgeBiasFrac { get; }

        public ScaledTunnelClassConfig(
            int startYMin,
            int startYMax,
            int endYMin,
            int endYMax,
            int countPerWidthTiles,
            int minCount,
            int maxCount,
            int countVariance,
            float minDiagonalDyOverDx,
            float minAngleDeg,
            float maxAngleDeg,
            int minDownwardDeltaYMin,
            int minDownwardDeltaYMax,
            float edgeBiasFrac
        )
        {
            StartYMin = startYMin;
            StartYMax = startYMax;
            EndYMin = endYMin;
            EndYMax = endYMax;
            CountPerWidthTiles = countPerWidthTiles;
            MinCount = minCount;
            MaxCount = maxCount;
            CountVariance = countVariance;
            MinDiagonalDyOverDx = minDiagonalDyOverDx;
            MinAngleDeg = minAngleDeg;
            MaxAngleDeg = maxAngleDeg;
            MinDownwardDeltaYMin = minDownwardDeltaYMin;
            MinDownwardDeltaYMax = minDownwardDeltaYMax;
            EdgeBiasFrac = edgeBiasFrac;
        }
    }
}