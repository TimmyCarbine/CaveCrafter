using Godot;

/// <summary>
/// Debug overlay: draws chunk bounds + chunk coordinate labels.
/// Matches ChunkRenderer's visible window rules (wrap X, clamp Y).
/// </summary>
public partial class TerrainDebugOverlay : Node2D
{
    // --- REFERENCES ---
    [Export] private NodePath worldGeneratorPath;
    [Export] private NodePath chunkRendererPath;
    [Export] private NodePath cameraPath;

    // --- VISUAL TUNING ---
    [Export] public float LineWidth = 2.0f;
    [Export] public int FontSize = 14;

    private WorldGenerator _worldGen;
    private ChunkRenderer _renderer;
    private Camera2D _camera;

    private int _chunkWpx;
    private int _chunkHpx;
    private int _worldWidthPx;

    public override void _Ready()
    {
        _worldGen = GetNodeOrNull<WorldGenerator>(worldGeneratorPath);
        if (_worldGen == null) GD.PushError("TerrainDebugOverlay: worldGeneratorPath is missing / invalid.");

        _renderer = GetNodeOrNull<ChunkRenderer>(chunkRendererPath);
        if (_renderer == null) GD.PushError("TerrainDebugOverlay: chunkRendererPath is missing / invalid.");

        _camera = GetNodeOrNull<Camera2D>(cameraPath);
        if (_camera == null)
        {
            _camera = GetViewport().GetCamera2D();
            GD.PushWarning("TerrainDebugOverlay: cameraPath is missing. Using viewport camera.");
        }

        if (_worldGen == null || _renderer == null || _renderer.TileSet == null || _camera == null)
            return;

        Vector2I tileSize = _renderer.TileSet.TileSize;

        _chunkWpx = WorldConstants.CHUNK_W * tileSize.X;
        _chunkHpx = WorldConstants.CHUNK_H * tileSize.Y;
        _worldWidthPx = _worldGen.WorldWidthTiles * tileSize.X;

        // Ensure this draws over the world.
        ZIndex = 10;
        ZAsRelative = false;

        Visible = false;
        SetPhysicsProcess(false);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("debug_terrain_overlay_toggle"))
        {
            Visible = !Visible;
            SetPhysicsProcess(Visible);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Redraw continuously so it tracks the camera in realtime.
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_worldGen == null || _renderer == null || _camera == null) return;
        if (_worldGen.World == null || _renderer.TileSet == null) return;

        Vector2 camPos = _camera.GlobalPosition;
        Vector2I tileSize = _renderer.TileSet.TileSize;

        // Camera tile coords.
        int camTileX = Mathf.FloorToInt(camPos.X / tileSize.X);
        int camTileY = Mathf.FloorToInt(camPos.Y / tileSize.Y);

        // Wrap X in tile-space (planet loop).
        camTileX = _worldGen.World.WrapX(camTileX);

        // Camera chunk coords.
        int camChunkX = camTileX / WorldConstants.CHUNK_W;
        int camChunkY = camTileY / WorldConstants.CHUNK_H;

        int radiusX = _renderer.RadiusChunksX;
        int radiusY = _renderer.RadiusChunksY;

        for (int oy = -radiusY; oy <= radiusY; oy++)
        {
            for (int ox = -radiusX; ox <= radiusX; ox++)
            {
                int wantChunkX = camChunkX + ox;
                int wantChunkY = camChunkY + oy;

                // Wrap X chunk index.
                wantChunkX = Mathf.PosMod(wantChunkX, _worldGen.World.ChunkCountX);

                // Clamp Y chunk index (no vertical wrap).
                if (wantChunkY < 0 || wantChunkY >= _worldGen.World.ChunkCountY)
                    continue;

                // Chunk's base world X (unwrapped).
                float baseX = wantChunkX * _chunkWpx;

                // Place at nearest image relative to camera X (pixel-space wrap).
                float placedX = WrapMath.NearestImageX(baseX, camPos.X, _worldWidthPx);
                float placedY = wantChunkY * _chunkHpx;

                Vector2 topLeft = new Vector2(placedX, placedY);
                Vector2 size = new Vector2(_chunkWpx, _chunkHpx);

                // Outline rect.
                DrawRect(new Rect2(topLeft, size), Colors.Yellow, filled: false, width: LineWidth);

                // Label (chunk coords).
                DrawString(
                    ThemeDB.FallbackFont,
                    topLeft + new Vector2(6, FontSize),
                    $"(X:{wantChunkX}, Y:{wantChunkY})",
                    HorizontalAlignment.Left,
                    -1,
                    FontSize,
                    Colors.White
                );
            }
        }
    }
}
