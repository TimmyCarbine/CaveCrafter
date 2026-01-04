using Godot;

/// <summary>
/// CaveCarver is a pure helper that mutates WorldData to carve caves.
/// It has no rendering logic or scene dependancies.
/// </summary>
public static class CaveCarver
{
    // === SETTINGS STRUCT ===
    public struct Settings
    {
        // Backbone Walkers
        public int BackboneWalkers;
        public int StepsMin;
        public int StepsMax;

        // Vertical Bounds (Tiles)
        public int MinY;
        public int MaxYPadding;

        // Tunnel Size
        public int MinRadius;
        public int MaxRadius;

        // Movement Behaviour
        public float HorizontalBias;
        public float SlopeChance;
        public float TurnAroundChance;

        // Debug
        public bool RecordPaths;
        public int PathSampleStep;
    }

    /// <summary>
    /// Carves the main cave network using long lived, horizontal biased walkers.
    /// Produces readable tunnels with gentle slopes and lots of intersection.
    /// </summary>
    public static void CarveBackbone(
        WorldData world,
        RandomNumberGenerator rng,
        int worldWidthTiles,
        int worldHeightTiles,
        int bedrockThickness,
        Settings s,
        System.Collections.Generic.List<Godot.Collections.Array<Vector2>> outPaths = null)
    {
        if (world == null)
        {
            GD.PushError("CaveCarver.CarveBackbone: world is null.");
            return;
        }
        if (rng == null)
        {
            GD.PushError("CaveCarver.CarveBackbone: rng is null.");
            return;
        }

        int maxY = worldHeightTiles - bedrockThickness - s.MaxYPadding;
        int minY = Mathf.Clamp(s.MinY, 0, worldHeightTiles - 1);
        maxY = Mathf.Clamp(maxY, 0, worldHeightTiles - 1);

        if (minY >= maxY)
        {
            GD.PushWarning("CaveCarver.CarveBackbone: Cave Y range invalid. Check MinY / MaxYPadding.");
            return;
        }

        int sampleStep = Mathf.Max(1, s.PathSampleStep);

        for (int i = 0; i < s.BackboneWalkers; i++)
        {
            int x = rng.RandiRange(0, worldWidthTiles - 1);
            int y = rng.RandiRange(minY, maxY);

            bool shouldRecord = s.RecordPaths && outPaths != null;
            var path = shouldRecord ? new Godot.Collections.Array<Vector2>() : null;

            // Track X unwrapped so the drawn line is continuous (no seam jumps)
            int xUnwrapped = x;

            // Initial horizontal direction
            int dirX = rng.Randf() < 0.5f ? -1 : 1;

            int steps = rng.RandiRange(s.StepsMin, s.StepsMax);
            int radius = rng.RandiRange(s.MinRadius, s.MaxRadius);

            if (shouldRecord) path.Add(new Vector2(xUnwrapped, y));

            for (int step = 0; step < steps; step++)
            {
                // Carve using wrapped coordinates
                CarveTunnelAt(world, worldWidthTiles, worldHeightTiles, bedrockThickness, x, y, radius);

                // Record path occasionally to keep it light
                if (shouldRecord && (step % sampleStep == 0))
                    path.Add(new Vector2(xUnwrapped, y));

                // Creates occasional dead ends / loops without becoming a maze
                if (rng.Randf() < s.TurnAroundChance) dirX *= -1;

                // Movement decision
                bool moveHori = rng.Randf() < s.HorizontalBias;

                if (moveHori)
                {
                    xUnwrapped += dirX;
                }
                else
                {
                    // Gentle slope
                    if (rng.Randf() < s.SlopeChance)
                        y += rng.Randf() < 0.5f ? -1 : 1;
                    else
                        xUnwrapped += dirX;
                }

                x = Mathf.PosMod(xUnwrapped, worldWidthTiles);
                y = Mathf.Clamp(y, minY, maxY);
            }

            if (shouldRecord)
            {
                path.Add(new Vector2(xUnwrapped, y));
                if (path.Count > 1) outPaths.Add(path);
            }
        }
    }

    /// <summary>
    /// Carves a small, readable tunnel cross section around (x, y).
    /// Current profile: tall enough slightly thicker at the centre.
    /// </summary>
    private static void CarveTunnelAt(
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
}