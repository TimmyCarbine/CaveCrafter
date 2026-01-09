namespace CaveCrafter.CaveGen.Api
{
    /// <summary>
    /// Generator phases. Split RNG streams by pahse so tweaks in later phases
    /// don't reshuffle earlier phases (determainism & better iteration).
    /// </summary>
    public enum CaveGenPahse
    {
        HighwayAnchors = 0,
        HighwaySplines = 1,
        ConnectorIntent = 2,
        ConnectorSplines = 3,
        Links = 4,
        Raster = 5,
    }
}