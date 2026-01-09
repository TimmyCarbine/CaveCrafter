using CaveCrafter.CaveGen.Api;
using CaveCrafter.CaveGen.Config;
using CaveCrafter.CaveGen.Debug;
using Godot;

/// <summary>
/// DATA-ONLY world generation.
/// Generates tiles into WorldData (chunked store).
/// Rendering is handled exclusively by ChunkRenderer.
///
/// Cave system:
/// - Old walker/branch carvers removed.
/// - New CC-Spline pipeline Phase 1 added (Highway intent generation only).
///   Phase 1 produces debug lines only (no carving).
///
/// Debug:
/// - Legacy cave debug overlay removed.
/// - We'll implement a terrain-only debug overlay later using the same pipeline pattern.
/// </summary>
public partial class WorldGenerator : Node
{
    // --- WORLD SIZE (TILES) ---
    private const int WORLD_WIDTH = 512;
    private const int WORLD_HEIGHT = 256;

    // --- SURFACE SETTINGS ---
    private const int MIN_SURFACE_Y = 20;                   // Smaller = higher surface (Godot Y grows downward)
    private const int MAX_SURFACE_Y = 60;                   // Larger = lower surface
    private const int START_SURFACE_Y = 40;

    private const float SURFACE_STEP_UP_CHANCE = 0.2f;      // -1 in Y (surface goes up)
    private const float SURFACE_STEP_FLAT_CHANCE = 0.6f;    // 0 in Y

    private const int SMOOTHING_PASSES = 2;
    private const int MAX_STEP_DELTA = 2;

    // --- MATERIAL LAYERS ---
    private const int DIRT_DEPTH = 25;                      // Tiles below surface that remain dirt
    private const int BEDROCK_THICKNESS = 3;                // Tiles at bottom that are bedrock

    // --- TILE SIZE (PX) ---
    // Used for spawning and cave debug overlay conversion.
    private const int TILE_SIZE = 32;

    // Optional seed to repeat worlds (0 = random)
    [Export] private int _seed = 0;

    [Export] private CharacterBody2D _player;

    // Where to spawn horizontally (tile coords). Default: middle of map.
    [Export] private int _spawnX = -1;

    // Cave generation config asset (Resource pipeline).
    [Export]
    public CaveGenConfigResource CaveConfig { get; set; }

    // New cave debug overlay (Phase 1 draws intent lines).
    // Assign in inspector if you want to see the Phase 1 debug.
    [Export]
    public CaveDebugOverlay CaveDebugOverlay { get; set; }

    // === REGENERATE CONTROLS ===
    [Export] private bool _incrementSeedOnRegenerate = false;
    [Export] private bool _regenerateCavesOnReady = true;

    // --- REFERENCES ---
    private WorldData _world;
    public WorldData World => _world;

    public int WorldWidthTiles => WORLD_WIDTH;
    public int WorldHeightTiles => WORLD_HEIGHT;

    // Internal RNG (terrain generation).
    private RandomNumberGenerator _rng;
    private int[] _surfaceY;

    // The resolved seed actually used this run (important when _seed == 0).
    private int _resolvedSeed;

    private bool _caveOverlayEnabled = true;
    private CaveDebugDrawFlags _cycleFlags = CaveDebugDrawFlags.IntentLines;

    public override void _Ready()
    {
        GetTree().Root.GrabFocus();

        // Initialise RNG (terrain generation uses this RNG).
        _rng = new RandomNumberGenerator();
        if (_seed == 0)
        {
            _rng.Randomize();
            _resolvedSeed = unchecked((int)_rng.Seed);
        }
        else
        {
            _rng.Seed = unchecked((ulong)_seed);
            _resolvedSeed = _seed;
        }

        if (CaveConfig == null)
        {
            GD.PushError("WorldGenerator: CaveConfig is not assigned.");
            return;
        }

        WarnIfMissingInputAction("debug_cave_regenerate");
        WarnIfMissingInputAction("debug_cave_regenerate_new_seed");
        WarnIfMissingInputAction("debug_cave_overlay_toggle");
        WarnIfMissingInputAction("debug_cave_overlay_cycle");

        GD.Print($"WorldGenerator: Using Seed={_resolvedSeed} (Original Export Seed={_seed})");
        GD.Print($"WorldGenerator: Loaded CaveGen config. Highway Start band = {CaveConfig.Highway.StartYMinFrac} - {CaveConfig.Highway.StartYMaxFrac}");
        GD.Print($"WorldGenerator: Loaded CaveGen config. Highway End band = {CaveConfig.Highway.EndYMinFrac} - {CaveConfig.Highway.EndYMaxFrac}");

        GenerateWorldDataOnly();

        if (_regenerateCavesOnReady)
        {
            // Phase 1 cave generation (debug-only).
            GenerateCavesPhase1_IntentsOnly();
        }

        SpawnPlayerOnSurface();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // Regenerate caves (force new seed)
        if (Input.IsActionJustPressed("debug_cave_regenerate_new_seed"))
        {
            RegenerateCavesPhase1(incrementSeed: true);
            return;
        }

        // Regenerate caves (same seed)
        if (Input.IsActionJustPressed("debug_cave_regenerate"))
        {
            RegenerateCavesPhase1(incrementSeed: _incrementSeedOnRegenerate);
            return;
        }

        // Toggle overlay
        if (Input.IsActionJustPressed("debug_cave_overlay_toggle"))
        {
            ToggleCaveOverlay();
            return;
        }

        // Cycle overlay flags
        if (Input.IsActionJustPressed("debug_cave_overlay_cycle"))
        {
            CycleCaveOverlayFlags();
            return;
        }
    }

    /// <summary>
    /// Generates world terrain into WorldData (data-only).
    /// Rendering is handled by ChunkRenderer elsewhere.
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
                // Bedrock band at bottom.
                if (y >= WORLD_HEIGHT - BEDROCK_THICKNESS)
                {
                    _world.SetTerrain(x, y, TileIds.BEDROCK);
                    continue;
                }

                // Air above surface.
                if (y < surface)
                {
                    _world.SetTerrain(x, y, TileIds.AIR);
                    continue;
                }

                // Dirt layer then stone below.
                int depthBelowSurface = y - surface;

                _world.SetTerrain(
                    x,
                    y,
                    depthBelowSurface <= DIRT_DEPTH ? TileIds.DIRT : TileIds.STONE
                );
            }
        }
    }

    /// <summary>
    /// Phase 1 (CC-Spline-1): Generate highway anchors + endpoints (intents only).
    /// No carving, no splines, just debug intent lines (via CaveDebugOverlayNode).
    /// </summary>
    private void GenerateCavesPhase1_IntentsOnly()
    {
        CaveGenRequest req = new CaveGenRequest(
            mapWidth: WORLD_WIDTH,
            mapHeight: WORLD_HEIGHT,
            seed: _resolvedSeed,
            configResource: CaveConfig
        );

        CaveGenResult result = CaveGenerator.GeneratePhase1(req);

        if (CaveDebugOverlay != null)
        {
            // Ensure overlay uses the same tile size your world uses.
            CaveDebugOverlay.PixelsPerTile = TILE_SIZE;
            CaveDebugOverlay.SetResult(result);
            CaveDebugOverlay.Visible = _caveOverlayEnabled;
        }

        GD.Print($"WorldGenerator: Phase 1 generated {result.HighwayIntents.Count} highway intents.");
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
        int spawnTileY = surfaceY - 2;

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
    /// Creates a rolling surface using a random walk, then optionally smooths it.
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
            float roll = _rng.Randf();

            int step;
            if (roll < SURFACE_STEP_UP_CHANCE)
                step = -1;
            else if (roll < SURFACE_STEP_UP_CHANCE + SURFACE_STEP_FLAT_CHANCE)
                step = 0;
            else
                step = 1;

            int next = current + step;

            next = Mathf.Clamp(next, MIN_SURFACE_Y, MAX_SURFACE_Y);

            int delta = next - current;
            delta = Mathf.Clamp(delta, -MAX_STEP_DELTA, MAX_STEP_DELTA);
            next = current + delta;

            current = next;
            heights[x] = current;
        }

        MakeHeightmapSeamless(heights);

        for (int pass = 0; pass < SMOOTHING_PASSES; pass++)
        {
            heights = SmoothHeightmap(heights);
            MakeHeightmapSeamless(heights);
        }

        return heights;
    }

    /// <summary>
    /// Smooths a heightmap by averaging each point with its immediate neighbours,
    /// treating the array as CIRCULAR so x=0 neighbours x=last (wraparound planet).
    /// </summary>
    private int[] SmoothHeightmap(int[] input)
    {
        int[] output = new int[input.Length];
        int lastIndex = input.Length - 1;

        for (int x = 0; x < input.Length; x++)
        {
            int leftIndex = (x == 0) ? lastIndex : (x - 1);
            int rightIndex = (x == lastIndex) ? 0 : (x + 1);

            int left = input[leftIndex];
            int mid = input[x];
            int right = input[rightIndex];

            int avg = Mathf.RoundToInt((left + mid + right) / 3.0f);

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
        int lastIndex = heights.Length - 1;
        int delta = heights[lastIndex] - heights[0];
        if (delta == 0) return;

        for (int x = 0; x < heights.Length; x++)
        {
            float t = (heights.Length == 1) ? 0f : (x / (float)lastIndex);
            int offset = Mathf.RoundToInt(delta * t);
            heights[x] = Mathf.Clamp(heights[x] - offset, MIN_SURFACE_Y, MAX_SURFACE_Y);
        }

        heights[lastIndex] = heights[0];
    }

    private void CycleCaveOverlayFlags()
    {
        if (CaveDebugOverlay == null)
        {
            GD.Print("WorldGenerator: No CaveDebugOverlay assigned, cannot cycle flags.");
            return;
        }

        // Cycle order:
        // 1) Lines
        // 2) Lines + Anchors
        // 3) Lines + Anchors + Endpoints
        // 4) Lines + Anchors + Endpoints + Labels
        // then back to Lines
        if (_cycleFlags == CaveDebugDrawFlags.IntentLines)
            _cycleFlags = CaveDebugDrawFlags.IntentLines | CaveDebugDrawFlags.Anchors;
        else if (_cycleFlags == (CaveDebugDrawFlags.IntentLines | CaveDebugDrawFlags.Anchors))
            _cycleFlags = CaveDebugDrawFlags.IntentLines | CaveDebugDrawFlags.Anchors | CaveDebugDrawFlags.Endpoints;
        else if (_cycleFlags == (CaveDebugDrawFlags.IntentLines | CaveDebugDrawFlags.Anchors | CaveDebugDrawFlags.Endpoints))
            _cycleFlags = CaveDebugDrawFlags.Phase1Default;
        else
            _cycleFlags = CaveDebugDrawFlags.IntentLines;

        CaveDebugOverlay.Flags = _cycleFlags;
        CaveDebugOverlay.QueueRedraw();

        GD.Print($"WorldGenerator: Cave overlay flags -> {_cycleFlags}");
    }

    private void RegenerateCavesPhase1(bool incrementSeed)
    {
        if (incrementSeed)
        {
            GD.Print($"WorldGenerator: Previous Seed -> {_resolvedSeed}");
            _rng.Randomize();
            _resolvedSeed = unchecked((int)_rng.Seed);
            GD.Print($"WorldGenerator: New Seed -> {_resolvedSeed}");
        }
        else
        {
            GD.Print($"WorldGenerator: Using Same Seed -> {_resolvedSeed}");
        }

        GenerateCavesPhase1_IntentsOnly();
    }

    private void ToggleCaveOverlay()
        {
        _caveOverlayEnabled = !_caveOverlayEnabled;

        if (CaveDebugOverlay != null)
        {
            CaveDebugOverlay.Visible = _caveOverlayEnabled;
        }

        GD.Print($"WorldGenerator: Cave overlay {(_caveOverlayEnabled ? "ENABLED" : "DISABLED")}");
    }

    private static void WarnIfMissingInputAction(string actionName)
    {
        if (!InputMap.HasAction(actionName))
        {
            GD.PushWarning($"WorldGenerator: Missing InputMap action '{actionName}'. Add it in Project Settings -> Input Map.");
        }
    }
}
