using Godot;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>
/// Generates a simple 2D tile based world.
///  - Random walkable surface heightmap (optionally smoothed)
///  - Dirt, Stone, Bedrock
///  - Writes results to a Tilemap (atlas based tiles)
/// </summary>

public partial class WorldGenerator : Node
{
    // --- WORLD SIZE (TILES) ---
    private const int WORLD_WIDTH = 256;
    private const int WORLD_HEIGHT = 128;

    // --- SURFACE SETTINGS ---
    private const int MIN_SURFACE_Y = 20;       // Smaller = higher surface (Godot Y grows downward)
    private const int MAX_SURFACE_Y = 60;       // Larger = lower surface
    private const int START_SURFACE_Y = 40;

    // Random walk probabilites (must add up to 1.0)
    // Higher FLAT keeps the surface calmer.

    private const float SURFACE_STEP_UP_CHANCE = 0.2f;      // -1 in Y (surface goes up)
    private const float SURFACE_STEP_FLAT_CHANCE = 0.6f;    // 0 in Y
    private const float SURFACE_STEP_DOWN_CHANCE = 0.2f;    // +1 in Y (surface goes down)

    // Optional smoothing passes for heightmap
    private const int SMOOTHING_PASSES = 2;

    // Limits suddden cliffs (after random walkable, before smoothing)
    private const int MAX_STEP_DELTA = 2;

    // --- MATERIAL LAYERS ---
    private const int DIRT_DEPTH = 8;                       // Tiles below surface that remain dirt
    private const int BEDROCK_THICKNESS = 3;                // Tiles at bottom that are bedrock

    // --- TILEMAP / TILESET MAPPING ---
    /// IMPORTANT:
    /// This script uses an Atlas source in the TileSet
    ///  - SOURCE_ID is the TileSet atlas source ID
    ///  - ATLAS coords identify which tile in that atlas to place

    // These must be matched in the TileSet layout
    private const int SOURCE_ID = 1;

    // Atlas coordinates (x,y) for each tile type in atlas
    private static readonly Vector2I DIRT_ATLAS = new Vector2I(0, 0);
    private static readonly Vector2I STONE_ATLAS = new Vector2I(1, 0);
    private static readonly Vector2I BEDROCK_ATLAS = new Vector2I(2, 0);

    // --- REFERENCES ---
    [Export] private NodePath tilemapGroundPath;
    private TileMapLayer _tilemapGround;
    
    // Optional seed to repeat worlds (0 = random)
    [Export] private int _seed = 0;

    [Export] private CharacterBody2D _player;
    [Export] private Camera2D _camera;

    // Where to spawn horizontally (tile coords). Default: middle of map.
    [Export] private int _spawnX = -1;

    // Internal RNG
    private RandomNumberGenerator _rng;

    private int[] _surfaceY;

    public override void _Ready()
    {
        GetTree().Root.GrabFocus();

        _tilemapGround = GetNodeOrNull<TileMapLayer>(tilemapGroundPath);
        if(_tilemapGround == null)
        {
            GD.PushError("WorldGenerator: TileMapGround is missing/invalid. Assign the TileMapLayer_Ground in the inspector.");
            return;
        }

        if(_tilemapGround.TileSet == null)
        {
            GD.PushError("WorldGenerator: The TileMapLayer has no TileSet assigned. Assign a TileSet to TileMapLayer_Ground.");
            return;
        }

        // Init RNG
        _rng = new RandomNumberGenerator();

        // If seed is 0, randomise; otherwise deterministic
        if(_seed == 0)
            _rng.Randomize();
        else
            _rng.Seed = (ulong)_seed;

        GenerateWorld();
        SpawnPlayerOnSurface();
        //_camera.CallDeferred("RebuildWorldBounds");
    }

    /// <summary>
    /// Generates and renders the world into the TileMapLayer.
    /// </summary>
    private void GenerateWorld()
    {
        // Clear previous tiles
        _tilemapGround.Clear();

        // Build surface heightmap (one Y value per X column)
        _surfaceY = BuildSurfaceHeightmap();

        // Fill the world column by column
        for (int x = 0; x < WORLD_WIDTH; x++)
        {
            int surface = _surfaceY[x];

            for (int y = 0; y < WORLD_HEIGHT; y++)
            {
                // Bedrock zone at bottom of the world
                if (y >= WORLD_HEIGHT - BEDROCK_THICKNESS)
                {
                    SetTile(x, y, BEDROCK_ATLAS);
                    continue;
                }

                // Above surface is air (no tile placed)
                if (y < surface) continue;

                // Depth below surface
                int depthBelowSurface = y - surface;

                // Dirt layer
                if (depthBelowSurface <= DIRT_DEPTH)
                {
                    SetTile(x, y, DIRT_ATLAS);
                }
                else
                {
                    SetTile(x, y, STONE_ATLAS);
                }
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
        Vector2I tileSize = _tilemapGround.TileSet.TileSize;

        // Place player roughly centered in the tile.
        Vector2 spawnPos = new Vector2(
            (spawnX * tileSize.X) + (tileSize.X / 2.0f),
            (spawnTileY * tileSize.Y)
        );

        _player.GlobalPosition = spawnPos;

        // Reset velocity so the player doesn't carry old motion.
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

        // Smooth the heightmap to reduce jaggedness
        for (int pass = 0; pass < SMOOTHING_PASSES; pass++)
        {
            heights = SmoothHeightmap(heights);
        }

        return heights;
    }

    /// <summary>
    /// Smooths a heightmap by averaging each point with its immediate neighbours
    /// </summary>
    private int[] SmoothHeightmap(int[] input)
    {
        int[] output = new int[input.Length];

        for (int x = 0; x < input.Length; x++)
        {
            int left = input[Mathf.Max(0, x - 1)];
            int mid = input[x];
            int right = input[Mathf.Min(input.Length - 1, x + 1)];

            // Average and round
            int avg = Mathf.RoundToInt((left + mid + right) / 3.0f);

            // Keep withi bounds
            output[x] = Mathf.Clamp(avg, MIN_SURFACE_Y, MAX_SURFACE_Y);
        }

        return output;
    }

    /// <summary>
    /// Sets a single tile in the TileMapLayer at cell (x,y) using atlas coords
    /// </summary>
    private void SetTile(int x, int y, Vector2I atlasCoords)
    {
        // TileMapLayer represents a single layer, so there's no layer index parameter
        // For atlas tiles: SetCell(coords, sourceId, atlasCoords, alternativeTile)
        _tilemapGround.SetCell(new Vector2I(x, y), SOURCE_ID, atlasCoords);
    }
}
