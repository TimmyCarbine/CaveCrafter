using Godot;

/// <summary>
/// Connector diggers.
/// Creates short lived mainly vertical links between backbone tunnels to form a navigatable network.
/// </summary>
public static class CaveCarverConnectors
{
    public struct Settings
    {
        public int Connectors;
        public int MaxLength;
        public float VerticalPhaseRatio;
        public int SearchRadiusForStart;
        public int MaxStartSearchTries;
        public int Radius;
        public int MinY;
        public int MaxYPadding;
    }

    public static void CarveConnectors(
        WorldData world,
        RandomNumberGenerator rng,
        int worldWidthTiles,
        int worldHeightTiles,
        int bedrockThickness,
        Settings s)
    {
        if (world == null)
        {
            GD.PushError("CaveCarverConnectors.CarveConnectors: world is null.");
            return;
        }

        if (rng == null)
        {
            GD.PushError("CaveCarverConnectors.CarveConnectors: rng is null.");
            return;
        }

        int minY = Mathf.Clamp(s.MinY, 0, worldHeightTiles - 1);
        int maxY = worldHeightTiles - bedrockThickness - s.MaxYPadding;
        maxY = Mathf.Clamp(maxY, 0, worldHeightTiles - 1);

        if (minY >= maxY)
        {
            GD.PushWarning("CaveCarverConnectors.CarveConnectors: Cave Y range invalid.Check MinY / MaxY.");
            return;
        }

        int maxLength = Mathf.Max(4, s.MaxLength);
        float verticalRatio = Mathf.Clamp(s.VerticalPhaseRatio, 0.0f, 1.0f);

        int startRadius = Mathf.Max(1, s.SearchRadiusForStart);
        int maxTries = Mathf.Max(1, s.MaxStartSearchTries);

        for (int i = 0; i < s.Connectors; i++)
        {
            int x = 0;
            int y = 0;

            bool foundStart = CaveCarverHelper.TryPickStartNearAir(
                world: world,
                rng: rng,
                worldWidthTiles: worldWidthTiles,
                worldHeightTiles: worldHeightTiles,
                minY: minY,
                maxY: maxY,
                searchRadius: startRadius,
                maxTries: maxTries,
                outX: out x,
                outY: out y
            );

            if (!foundStart) continue;

            int dirY = rng.Randf() < 0.5f ? -1 : 1;
            int dirX = rng.Randf() < 0.5f ? -1 : 1;

            int length = rng.RandiRange(maxLength / 2, maxLength);
            int verticalSteps = Mathf.RoundToInt(length * verticalRatio);

            // Walk: mostly vertical early, then bend horizontally
            for (int step = 0; step < length; step++)
            {
                CaveCarverHelper.CarveTunnelAt(
                world,
                worldWidthTiles,
                worldHeightTiles,
                bedrockThickness,
                x,
                y,
                s.Radius
                );

                if (step < verticalSteps)
                {
                    y += dirY;
                    if (rng.Randf() < 0.12f) x += dirX;
                }
                else
                {
                    x += dirX;
                    if (rng.Randf() < 0.1f) y += rng.Randf() < 0.5f ? -1 : 1;
                }

                x = Mathf.PosMod(x, worldWidthTiles);
                y = Mathf.Clamp(y, minY, maxY);

                // Stop if connected into existing air (after a couple of steps)
                if (step > 4 && world.GetTerrain(x, y) == TileIds.AIR) break;
            }
        }
    }
}