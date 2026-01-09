using Godot;

namespace CaveCrafter.CaveGen.Config
{
    /// <summary>
    /// Editor facing configuration for a single tunnel class (Highway / Connector / Link).
    /// This is a Godot Resource, meaning it can be saved as a .tres asset and edited in the inspector.
    /// </summary>

    [GlobalClass]
    public partial class TunnelClassConfigResource : Resource
    {
        // === BANDS (FRACTIONS OF MAP HEIGHT) ===
        [Export(PropertyHint.Range, "0,1,0.001")]
        public float StartYMinFrac { get; set; } = 0.1f;

        [Export(PropertyHint.Range, "0,1,0.001")]
        public float StartYMaxFrac { get; set; } = 0.2f;

        [Export(PropertyHint.Range, "0,1,0.001")]
        public float EndYMinFrac { get; set; } = 0.6f;

        [Export(PropertyHint.Range, "0,1,0.001")]
        public float EndYMaxFrac { get; set; } = 0.8f;

        // === COUNT RULES ===
        [Export(PropertyHint.Range, "1,4096,1")]
        public int CountPerWidthTiles { get; set; } = 140;

        [Export(PropertyHint.Range, "1,64,1")]
        public int MinCount { get; set; } = 4;

        [Export(PropertyHint.Range, "1,64,1")]
        public int MaxCount { get; set; } = 8;

        /// <summary>
        /// Random variance applied to the computed base count.
        /// Examplle: base=5, variance=1 => {4,5,6}.
        /// </summary>
        [Export(PropertyHint.Range, "0,8,1")]
        public int CountVariance { get; set; } = 1;

        // === INTENT RULES ===
        /// <summary>
        /// Stops the tunnels from forming horizontally.
        /// </summary>
        [Export(PropertyHint.Range, "0,2,0.01")]
        public float MinDiagonalDyOverDx { get; set; } = 0.35f;

        /// <summary>
        /// Angle constraints in DEGREES for readability.
        /// Enforce by adjusting endX making tunnel longer but keeping angle within band.
        /// </summary>
        [Export(PropertyHint.Range, "0,89,1")]
        public float MinAngleDeg { get; set; } = 20f;

        [Export(PropertyHint.Range, "0,89,1")]
        public float MaxAngleDeg { get; set; } = 40f;

        // === NET DOWNWARD TREND ===
        [Export(PropertyHint.Range, "0,1,0.01")]
        public float MinDownwardDeltaYFracMin { get; set; } = 0.18f;

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float MinDownwardDeltaYFracMax { get; set; } = 0.30f;

        // === COVERAGE ===
        [Export(PropertyHint.Range, "0,0.5,0.001")]
        public float EdgeBiasFrac { get; set; } = 0.12f;
    }
}