using Godot;

/// <summary>
/// DATA-ONLY world generation.
/// Generates tiles into WorldData (chunked store).
/// Rendering is handled exclusively by ChunkRenderer.
/// </summary>

public partial class WorldGenerator : Node
{
    // --- WORLD SIZE (TILES) ---
    private const int WORLD_WIDTH = 256;
    private const int WORLD_HEIGHT = 128;

    // --- SURFACE SETTINGS ---
    private const int MIN_SURFACE_Y = 20;                   // Smaller = higher surface (Godot Y grows downward)
    private const int MAX_SURFACE_Y = 60;                   // Larger = lower surface
    private const int START_SURFACE_Y = 40;

    private const float SURFACE_STEP_UP_CHANCE = 0.2f;      // -1 in Y (surface goes up)
    private const float SURFACE_STEP_FLAT_CHANCE = 0.6f;    // 0 in Y
    private const float SURFACE_STEP_DOWN_CHANCE = 0.2f;    // +1 in Y (surface goes down)

    private const int SMOOTHING_PASSES = 2;
    private const int MAX_STEP_DELTA = 2;

    // --- MATERIAL LAYERS ---
    private const int DIRT_DEPTH = 8;                       // Tiles below surface that remain dirt
    private const int BEDROCK_THICKNESS = 3;                // Tiles at bottom that are bedrock

    // Optional seed to repeat worlds (0 = random)
    [Export] private int _seed = 0;
    
    [Export] private CharacterBody2D _player;

    // Where to spawn horizontally (tile coords). Default: middle of map.
    [Export] private int _spawnX = -1;

    // Atlas coordinates (x,y) for each tile type in atlas
    private static readonly Vector2I DIRT_ATLAS = new Vector2I(0, 0);
    private static readonly Vector2I STONE_ATLAS = new Vector2I(1, 0);
    private static readonly Vector2I BEDROCK_ATLAS = new Vector2I(2, 0);

    // --- REFERENCES ---
    private WorldData _world;

    public WorldData World => _world;
    public int WorldWidthTiles => WORLD_WIDTH;
    public int WorldHeightTiles => WORLD_HEIGHT;

    // Internal RNG
    private RandomNumberGenerator _rng;
    private int[] _surfaceY;

    public override void _Ready()
    {
        GetTree().Root.GrabFocus();

        _rng = new RandomNumberGenerator();
        if (_seed == 0) _rng.Randomize();
        else _rng.Seed = (ulong)_seed;

        GenerateWorldDataOnly();
        SpawnPlayerOnSurface();
    }

    /// <summary>
    /// Generates and renders the world into the TileMapLayer.
    /// </summary>
    private void GenerateWorldDataOnly()
    {
        _world = new WorldData(WORLD_WIDTH, WORLD_HEIGHT);

        _surfaceY = BuildSurfaceHeightmap();

        for (int x = 0; x < WORLD_WIDTH; x++)
        {
            int surface = _surfaceY[x];

            for (int y = 0; y < WORLD_HEIGHT; y++)
            {
                if (y >= WORLD_HEIGHT - BEDROCK_THICKNESS)
                {
                    _world.SetTerrain(x, y, TileIds.BEDROCK);
                    continue;
                }

                if (y < surface)
                {
                    _world.SetTerrain(x, y, TileIds.AIR);
                    continue;
                }

                int depthBelowSurface = y - surface;

                _world.SetTerrain(
                    x,
                    y,
                    depthBelowSurface <= DIRT_DEPTH ? TileIds.DIRT : TileIds.STONE
                );
            }
        }
    }

    private void SpawnPlayerOnSurface()
    {
        // If there's no player assigned, just skip.
        if (_player == null)
        {
            GD.Print("WorldGenerator: No player assigned, skipping spawn.");
            return;
        }

        // Choose spawn X.
        int spawnX = _spawnX;
        if (spawnX < 0)
            spawnX = WORLD_WIDTH / 2;

        spawnX = Mathf.Clamp(spawnX, 0, WORLD_WIDTH - 1);

        // Surface tile Y at that column.
        int surfaceY = _surfaceY[spawnX];

        // Spawn the player a little ABOVE the surface so they fall onto it cleanly.
        // (surface tile is solid at y == surfaceY, so we put the player above that)
        int spawnTileY = surfaceY - 2;

        // Convert tile coords to world pixels.
        // TileMapLayer uses tile size from the TileSet.
        const int TILE_SIZE = 32;

        // Place player roughly centered in the tile.
        Vector2 spawnPos = new Vector2(
            (spawnX * TILE_SIZE) + (TILE_SIZE / 2.0f),
            spawnTileY * TILE_SIZE
        );

        _player.GlobalPosition = spawnPos;
        _player.Velocity = Vector2.Zero;

        GD.Print($"WorldGenerator: Spawned player at tile ({spawnX},{spawnTileY}) px {spawnPos}");
    }


    /// <summary>
    /// Creates a rolling surface using a random walk, then optionally smooths it
    /// </summary>
    private int[] BuildSurfaceHeightmap()
    {
        int[] heights = new int[WORLD_WIDTH];

        // Start height
        int current = Mathf.Clamp(START_SURFACE_Y, MIN_SURFACE_Y, MAX_SURFACE_Y);
        heights[0] = current;

        // Random walk across X
        for (int x = 1; x < WORLD_WIDTH; x++)
        {
            // Pick a step based on probabilites
            float roll = _rng.Randf();

            // Up means surface Y decreases (goes up visually)
            int step;
            if (roll < SURFACE_STEP_UP_CHANCE)
                step = -1;
            else if (roll < SURFACE_STEP_UP_CHANCE + SURFACE_STEP_FLAT_CHANCE)
                step = 0;
            else
                step = 1;

            int next = current + step;

            // Clamp to surface bounds
            next = Mathf.Clamp(next, MIN_SURFACE_Y, MAX_SURFACE_Y);

            // Limit sudden cliffs
            int delta = next - current;
            delta = Mathf.Clamp(delta, -MAX_STEP_DELTA, MAX_STEP_DELTA);
            next = current + delta;

            current = next;
            heights[x] = current;
        }

        // Seamless ends
        MakeHeightmapSeamless(heights);

        // Smooth the heightmap to reduce jaggedness
        for (int pass = 0; pass < SMOOTHING_PASSES; pass++)
        {
            heights = SmoothHeightmap(heights);
            MakeHeightmapSeamless(heights);
        }

        return heights;
    }

    //// <summary>
    /// Smooths a heightmap by averaging each point with its immediate neighbours,
    /// treating the array as CIRCULAR so x=0 neighbours x=last (wraparound planet).
    /// </summary>
    private int[] SmoothHeightmap(int[] input)
    {
        int[] output = new int[input.Length];
        int lastIndex = input.Length - 1;

        for (int x = 0; x < input.Length; x++)
        {
            // Wrap neighbours so the seam is smoothed consistently.
            int leftIndex = (x == 0) ? lastIndex : (x - 1);
            int rightIndex = (x == lastIndex) ? 0 : (x + 1);

            int left = input[leftIndex];
            int mid = input[x];
            int right = input[rightIndex];

            // Average and round
            int avg = Mathf.RoundToInt((left + mid + right) / 3.0f);

            // Keep within bounds
            output[x] = Mathf.Clamp(avg, MIN_SURFACE_Y, MAX_SURFACE_Y);
        }

        return output;
    }

    /// <summary>
    /// Forces the heightmap to be seamless by removing the height difference between the first and last column.
    /// This prevents a "step" at the wrap seam (x=0 <-> x=WORLD_WIDTH-1).
    /// </summary>
    private void MakeHeightmapSeamless(int[] heights)
    {
        // If the endpoints already match, we're done.
        int lastIndex = heights.Length - 1;
        int delta = heights[lastIndex] - heights[0];
        if (delta == 0) return;

        // Linearly distribute the delta across the whole width so the last equals the first.
        // This avoids a hard correction at just one column.
        for (int x = 0; x < heights.Length; x++)
        {
            float t = (heights.Length == 1) ? 0f : (x / (float)lastIndex);
            int offset = Mathf.RoundToInt(delta * t);
            heights[x] = Mathf.Clamp(heights[x] - offset, MIN_SURFACE_Y, MAX_SURFACE_Y);
        }

        // Ensure exact equality at the seam after rounding/clamping.
        heights[lastIndex] = heights[0];
    }
}
