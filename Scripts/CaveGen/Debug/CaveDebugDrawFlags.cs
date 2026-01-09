using System;

namespace CaveCrafter.CaveGen.Debug
{
    /// <summary>
    /// Debug draw flags. Kept separate so overlays can be combined.
    /// </summary>
    [Flags]
    public enum CaveDebugDrawFlags
    {
        None = 0,

        Anchors = 1 << 0,
        Endpoints = 1 << 1,
        IntentLines = 1 << 2,
        Labels = 1 << 3,

        Phase1Default = Anchors | Endpoints | IntentLines | Labels,
    }
}