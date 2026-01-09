using CaveCrafter.CaveGen.Core.Geometry;

namespace CaveCrafter.CaveGen.Core.Planning
{
    /// <summary>
    /// A highway's planned start and end before building the spline.
    /// This is used to draw the debug overlay (anchors, endpoints & intent lines).
    /// </summary>
    public sealed class HighwayIntent
    {
        /// <summary>Stable identifier for debug labels and future linking.</summary>
        public int HighwayId { get; }

        /// <summary>Start point in tile-space coordinates (float for future spline compatibility).</summary>
        public Float2 Start { get; }

        /// <summary>End point in tile-space coordinates {float for future spline compatibility).</summary>
        public Float2 End { get; }

        /// <summary>
        /// For debugging: which direction the highway was biased to travel horizontally.
        /// </summary>
        public HighwaySide Side { get; }

        public HighwayIntent(int highwayId, Float2 start, Float2 end, HighwaySide side)
        {
            HighwayId = highwayId;
            Start = start;
            End = end;
            Side = side;
        }
    }

    /// <summary>
    /// Debug-only label for which side this highway decided to slope.
    /// </summary>
    public enum HighwaySide
    {
        Left = 0,
        Right = 1,
    }
}