namespace CaveCrafter.CaveGen.Config
{
    /// <summary>
    /// Pure C# (engine independent) configuration used by the core generator.
    /// Built from Godot Resources so the core can be unit tested without Godot.
    /// </summary>
    public sealed class CaveGenConfig
    {
        public TunnelClassConfig Highway { get; }

        public CaveGenConfig(TunnelClassConfig highway)
        {
            Highway = highway;
        }
    }

    /// <summary>
    /// Pure C# tunnel class config (fractions & counts).
    /// </summary>
    public sealed class TunnelClassConfig
    {
        public float StartYMinFrac { get; }
        public float StartYMaxFrac { get; }

        public float EndYMinFrac { get; }
        public float EndYMaxFrac { get; }

        public int CountPerWidthTiles { get; }
        public int MinCount { get; }
        public int MaxCount { get; }
        public int CountVariance { get; }

        public float MinDiagonalDyOverDx { get; }

        public float MinAngleDeg { get; }
        public float MaxAngleDeg { get; }

        public float MinDownwardDeltaYFracMin { get; }
        public float MinDownwardDeltaYFracMax { get; }

        public float EdgeBiasFrac { get; }

        public TunnelClassConfig(
            float startYMinFrac,
            float startYMaxFrac,
            float endYMinFrac,
            float endYMaxFrac,
            int countPerWidthTiles,
            int minCount,
            int maxCount,
            int countVariance,
            float minDiagonalDyOverDx,
            float minAngleDeg,
            float maxAngleDeg,
            float minDownwardDeltaYFracMin,
            float minDownwardDeltaYFracMax,
            float edgeBiasFrac
        )
        {
            StartYMinFrac = startYMinFrac;
            StartYMaxFrac = startYMaxFrac;
            EndYMinFrac = endYMinFrac;
            EndYMaxFrac = endYMaxFrac;
            CountPerWidthTiles = countPerWidthTiles;
            MinCount = minCount;
            MaxCount = maxCount;
            CountVariance = countVariance;
            MinDiagonalDyOverDx = minDiagonalDyOverDx;
            MinAngleDeg = minAngleDeg;
            MaxAngleDeg = maxAngleDeg;
            MinDownwardDeltaYFracMin = minDownwardDeltaYFracMin;
            MinDownwardDeltaYFracMax = minDownwardDeltaYFracMax;
            EdgeBiasFrac = edgeBiasFrac;
        }
    }
}