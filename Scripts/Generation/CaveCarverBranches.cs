using Godot;

/// <summary>
/// Branch diggers.
/// Adds short branches from existing tunnels to create intersections and occasional dead ends.
/// </summary>
public static class CaveCarverBranches
{
    public struct Settings
    {
        public int Branches;
        public int MinLength;
        public int MaxLength;
        public float HorizontalBias;
        public float SlopeChance;
        public float TurnAroundChance;
        public float EarlyStopChance;
        public int SearchRadiusForStart;
        public int MaxStartSearchTries;
        public int Radius;
        public int MinY;
        public int MaxYPadding;
    }

    public static void CarveBranches(
        WorldData world,
        RandomNumberGenerator rng,
        int worldWidthTiles,
        int worldHeightTiles,
        int bedrockThickness,
        Settings s)
    {
        if (world == null)
        {
            GD.PushError("CaveCarverBranches.CarveBranches: world is null.");
            return;
        }

        if (rng == null)
        {
            GD.PushError("CaveCarverBranches.CarveBranches: rng is null.");
            return;
        }

        int minY = Mathf.Clamp(s.MinY, 0, worldHeightTiles - 1);
        int maxY = worldHeightTiles - bedrockThickness - s.MaxYPadding;
        maxY = Mathf.Clamp(maxY, 0, worldHeightTiles - 1);

        if (minY >= maxY)
        {
            GD.PushWarning("CaveCarverBranches.CarveBranches: Cave Y range invalid. Check MinY / MaxYPadding.");
            return;
        }

        int startRadius = Mathf.Max(1, s.SearchRadiusForStart);
        int maxTries = Mathf.Max(1, s.MaxStartSearchTries);

        int minLen = Mathf.Max(2, s.MinLength);
        int maxLen = Mathf.Max(minLen, s.MaxLength);

        for (int i = 0; i < s.Branches; i++)
        {
            int x;
            int y;

            if (!CaveCarverHelper.TryPickStartNearAir(world, rng, worldWidthTiles, worldHeightTiles, minY, maxY, startRadius, maxTries, out x, out y))
                continue;

            int dirX = rng.Randf() < 0.5f ? -1 : 1;
            int dirY = rng.Randf() < 0.5f ? -1 : 1;

            int length = rng.RandiRange(minLen, maxLen);

            for (int step = 0; step < length; step++)
            {
                CaveCarverHelper.CarveTunnelAt(world, worldWidthTiles, worldHeightTiles, bedrockThickness, x, y, s.Radius);

                // If we hit another tunnel, stop = intersection created.
                // (Wait a few steps so we don't immediately "hit" the start cave.)
                if (step > 3 && CaveCarverHelper.IsNearAir(world, worldWidthTiles, worldHeightTiles, x, y, 2))
                    break;

                // Chance to stop early to create dead ends
                if (step > 6 && rng.Randf() < s.EarlyStopChance)
                    break;

                if (rng.Randf() < s.TurnAroundChance) dirX *= -1;

                bool moveHori = rng.Randf() < s.HorizontalBias;

                if (moveHori)
                {
                    x += dirX;
                }
                else
                {
                    if (rng.Randf() < s.SlopeChance)
                        y += dirY;
                    else
                        x += dirX;
                }

                x = Mathf.PosMod(x, worldWidthTiles);
                y = Mathf.Clamp(y, minY, maxY);
            }
        }
    }
}