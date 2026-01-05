using Godot;

public static class CaveCarverHelper
{
    /// <summary>
    /// Carves a small, readable tunnel cross section around (x, y).
    /// Current profile: tall enough slightly thicker at the centre.
    /// </summary>
    public static void CarveTunnelAt(
        WorldData world,
        int worldWidthTiles,
        int worldHeightTiles,
        int bedrockThickness,
        int x,
        int y,
        int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int yy = y + dy;
            if (yy < 0 || yy >= worldHeightTiles) continue;
            if (yy >= worldHeightTiles - bedrockThickness) continue;

            int thickness = (dy == 0) ? 1: 0;

            for (int dx = -thickness; dx <= thickness; dx++)
            {
                int xx = Mathf.PosMod(x + dx, worldWidthTiles);
                world.SetTerrain(xx, yy, TileIds.AIR);
            }
        }
    }

    public static bool TryPickStartNearAir(
        WorldData world,
        RandomNumberGenerator rng,
        int worldWidthTiles,
        int worldHeightTiles,
        int minY,
        int maxY,
        int searchRadius,
        int maxTries,
        out int outX,
        out int outY)
    {
        for (int tries = 0; tries < maxTries; tries++)
        {
            int x = rng.RandiRange(0, worldWidthTiles - 1);
            int y = rng.RandiRange(minY, maxY);

            if (IsNearAir(world, worldWidthTiles, worldHeightTiles, x, y, searchRadius))
            {
                outX = x;
                outY = y;
                return true;
            }
        }

        outX = 0;
        outY = 0;
        return false;
    }

    public static bool IsNearAir(
        WorldData world,
        int worldWidthTiles,
        int worldHeightTiles,
        int x,
        int y,
        int r)
    {
        for (int dy = -r; dy <= r; dy++)
        {
            int yy = y + dy;
            if (yy < 0 || yy >= worldHeightTiles) continue;

            for (int dx = -r; dx <= r; dx++)
            {
                int xx = Mathf.PosMod(x + dx, worldWidthTiles);
                if (world.GetTerrain(xx, yy) == TileIds.AIR)
                {
                    return true;
                }
            }
        }
        return false;
    }
}