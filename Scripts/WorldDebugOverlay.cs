using Godot;

/// <summary>
/// Fast debug overlay:
/// - Chunk borders in the visible window
/// - Mouse probe: shows tile + chunk + tile id under cursor
/// No per-tile rendering. Designed to scale.
/// </summary>
public partial class WorldDebugOverlay : Node2D
{
    [Export] private NodePath worldGeneratorPath;
    [Export] private NodePath chunkRendererPath;
    [Export] private NodePath cameraPath; // optional

    [Export] public bool EnabledOverlay = true;
    [Export] public bool ShowChunkBorders = true;
    [Export] public bool ShowChunkCoords = false; // optional, slightly more cost
    [Export] public bool ShowMouseProbe = true;
    [Export] public bool ShowHoveredTile = true;
    [Export] public bool ShowCavePaths = true;

    [Export] public Color HoveredTileOutline = new Color(0.2f, 1f, 0.2f, 0.9f); // bright green
    [Export] public Color ChunkBorderColor = new Color(1f, 0.9f, 0.2f, 0.35f);
    [Export] public Color TextColor = new Color(1f, 1f, 1f, 0.9f);
    [Export] public Color CavePathColor = new Color(1f, 0.3f, 0.3f, 0.9f);

    private WorldGenerator _gen;
    private ChunkRenderer _renderer;
    private Camera2D _camera;

    private int _tileW;
    private int _tileH;
    private int _worldWidthPx;
    private int _chunkWpx;
    private int _chunkHpx;

    private Vector2I _lastCamChunk = new Vector2I(int.MinValue, int.MinValue);

    public override void _Ready()
    {
        _gen = GetNodeOrNull<WorldGenerator>(worldGeneratorPath);
        if (_gen == null) { GD.PushError("WorldDebugOverlay: worldGeneratorPath invalid."); return; }

        _renderer = GetNodeOrNull<ChunkRenderer>(chunkRendererPath);
        if (_renderer == null) { GD.PushError("WorldDebugOverlay: chunkRendererPath invalid."); return; }

        _camera = GetNodeOrNull<Camera2D>(cameraPath);
        if (_camera == null) _camera = GetViewport().GetCamera2D();
        if (_camera == null) { GD.PushError("WorldDebugOverlay: No camera found."); return; }

        Vector2I tileSize = _renderer.TileSet.TileSize;
        _tileW = tileSize.X;
        _tileH = tileSize.Y;

        _worldWidthPx = _gen.WorldWidthTiles * _tileW;
        _chunkWpx = WorldConstants.CHUNK_W * _tileW;
        _chunkHpx = WorldConstants.CHUNK_H * _tileH;

        TopLevel = true;

        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("toggle_debug_overlay"))
        {
            EnabledOverlay = !EnabledOverlay;
            QueueRedraw();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!EnabledOverlay) return;
        if (_gen?.World == null || _camera == null) return;

        // Redraw only when the camera changes chunk (cheap + responsive)
        Vector2 camPos = _camera.GlobalPosition;

        int camTileX = Mathf.FloorToInt(camPos.X / _tileW);
        int camTileY = Mathf.FloorToInt(camPos.Y / _tileH);
        camTileX = _gen.World.WrapX(camTileX);

        Vector2I camChunk = new Vector2I(camTileX / WorldConstants.CHUNK_W, camTileY / WorldConstants.CHUNK_H);

        if (camChunk != _lastCamChunk)
        {
            _lastCamChunk = camChunk;
            QueueRedraw();
        }

        // Also redraw when mouse probe is enabled (but still cheap)
        if (ShowMouseProbe)
            QueueRedraw();
    }

    public override void _Draw()
    {
        if (!EnabledOverlay) return;
        if (_gen?.World == null || _renderer == null || _camera == null) return;

        Vector2 camPos = _camera.GlobalPosition;

        int camTileX = Mathf.FloorToInt(camPos.X / _tileW);
        int camTileY = Mathf.FloorToInt(camPos.Y / _tileH);
        camTileX = _gen.World.WrapX(camTileX);

        int camChunkX = camTileX / WorldConstants.CHUNK_W;
        int camChunkY = camTileY / WorldConstants.CHUNK_H;

        int radiusX = _renderer.RadiusChunksX;
        int radiusY = _renderer.RadiusChunksY;

        if (ShowChunkBorders)
        {
            for (int oy = -radiusY; oy <= radiusY; oy++)
            for (int ox = -radiusX; ox <= radiusX; ox++)
            {
                int chunkX = Mathf.PosMod(camChunkX + ox, _gen.World.ChunkCountX);
                int chunkY = camChunkY + oy;

                if (chunkY < 0 || chunkY >= _gen.World.ChunkCountY)
                    continue;

                float placedX = NearestImageChunkX(chunkX, camPos.X);
                float placedY = chunkY * _chunkHpx;

                Rect2 r = new Rect2(new Vector2(placedX, placedY), new Vector2(_chunkWpx, _chunkHpx));
                DrawRect(r, ChunkBorderColor, filled: false, width: 2f);

                if (ShowChunkCoords)
                {
                    // Minimal text. If you want nicer labels later, we can swap to a Label node.
                    DrawString(
                        ThemeDB.FallbackFont,
                        new Vector2(placedX + 6, placedY + 18),
                        $"({chunkX},{chunkY})",
                        modulate: TextColor
                    );
                }
            }
        }

        if (ShowMouseProbe) DrawMouseProbe();
        if (ShowHoveredTile) DrawHoveredTileOutline();
        if (ShowCavePaths) DrawCavePaths();
    }

    private void DrawMouseProbe()
    {
        Camera2D cam = GetViewport().GetCamera2D();
        if (cam == null) return;

        Vector2 mouseWorld = cam.GetGlobalMousePosition();

        int tileX = Mathf.FloorToInt(mouseWorld.X / _tileW);
        int tileY = Mathf.FloorToInt(mouseWorld.Y / _tileH);

        int wrappedX = _gen.World.WrapX(tileX);

        int chunkX = wrappedX / WorldConstants.CHUNK_W;
        int chunkY = tileY / WorldConstants.CHUNK_H;

        ushort id = _gen.World.GetTerrain(tileX, tileY);

        string txt =
            $"Mouse Tile: ({tileX},{tileY})  WrappedX: {wrappedX}\n " +
            $"Chunk: ({chunkX},{chunkY})  Tile ID: {id}";

        // Draw a small text box near the cursor
        Vector2 pos = mouseWorld + new Vector2(16, -16);

        DrawString(ThemeDB.FallbackFont, pos, txt, modulate: TextColor);
    }

    private void DrawHoveredTileOutline()
    {
        Camera2D cam = GetViewport().GetCamera2D();
        if (cam == null) return;

        Vector2 mouseWorld = cam.GetGlobalMousePosition();

        int tileX = Mathf.FloorToInt(mouseWorld.X / _tileW);
        int tileY = Mathf.FloorToInt(mouseWorld.Y / _tileH);

        // Vertical bounds check (no Y wrap)
        if (tileY < 0 || tileY >= _gen.WorldHeightTiles)
            return;

        int wrappedX = _gen.World.WrapX(tileX);

        // Find nearest image in X so the outline matches what you see
        float drawX = NearestImageTileX(wrappedX, cam.GlobalPosition.X);
        float drawY = tileY * _tileH;

        Rect2 tileRect = new Rect2(
            new Vector2(drawX, drawY),
            new Vector2(_tileW, _tileH)
        );
        tileRect = tileRect.Grow(-1f);
        DrawRect(tileRect, HoveredTileOutline, filled: false, width: 2f);
    }

    private void DrawCavePaths()
    {
        if (_gen == null) return;
        if (_gen.CaveBackbonePaths == null) return;

        float camX = _camera.GlobalPosition.X;

        foreach (var path in _gen.CaveBackbonePaths)
        {
            if (path.Count < 2) continue;

            // Draw segments
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 aTile = path[i - 1];
                Vector2 bTile = path[i];

                Vector2 aPx = TileToWorldPxWrapped(aTile, camX);
                Vector2 bPx = TileToWorldPxWrapped(bTile, camX);

                DrawLine(aPx, bPx, CavePathColor, 2f);
            }
        }
    }

    private float NearestImage(float baseX, float cameraX)
    {
        float a = baseX;
        float b = baseX - _worldWidthPx;
        float c = baseX + _worldWidthPx;

        float da = Mathf.Abs(a - cameraX);
        float db = Mathf.Abs(b - cameraX);
        float dc = Mathf.Abs(c - cameraX);

        if (db < da && db <= dc) return b;
        if (dc < da && dc < db) return c;
        return a;
    }

    private float NearestImageChunkX(int wrappedChunkX, float cameraX)
    {
        float baseX = wrappedChunkX * _chunkWpx;
        return NearestImage(baseX, cameraX);
    }

    private float NearestImageTileX(int wrappedTileX, float cameraX)
    {
        float baseX = wrappedTileX * _tileW;
        return NearestImage(baseX, cameraX);
    }

    private Vector2 TileToWorldPxWrapped(Vector2 tile, float cameraX)
    {
        // tile.X is UNWRAPPED tile X (can be negative or > width)
        float xPx = tile.X * _tileW;
        float yPx = tile.Y * _tileH;

        // Wrap the pixel x to the nearest image relative to camera
        float wrappedXPx = WrapToNearest(xPx, cameraX, _worldWidthPx);

        // Centre it in the tile for nicer lines
        wrappedXPx += _tileW * 0.5f;
        yPx += _tileH * 0.5f;

        return new Vector2(wrappedXPx, yPx);
    }

    private static float WrapToNearest(float x, float anchorX, float period)
    {
        // Shift x by multiples of period so it is closest to anchorX
        float k = Mathf.Round((x - anchorX) / period);
        return x - (k * period);
    }
}
