using Godot;

/// <summary>
/// Renders only a window of chunks around the camera using a fixed pool of TileMapLayers.
/// This is the ONLY rendering path (no old full-world rendering).
/// </summary>
public partial class ChunkRenderer : Node2D
{
    // === REFERENCES ===
    [Export] private NodePath worldGeneratorPath;
    private WorldGenerator _worldGen;

    [Export] private NodePath cameraPath;
    private Camera2D _camera;

    // VIEW WINDOW (in chunks) ===
    // Total rendered = (2*RadiusX+1) by (2*RadiusY +1)
    [Export] public int RadiusChunksX = 4; // 9 Chunks Wide
    [Export] public int RadiusChunksY = 3; // 7 Chunks High

    // === TILESET SOURCE ===
    [Export] public TileSet TileSet;
    [Export] public int SourceId = 1;

    [Export] public Vector2I DirtAtlas = new Vector2I(0, 0);
    [Export] public Vector2I StoneAtlas = new Vector2I(1, 0);
    [Export] public Vector2I BedrockAtlas = new Vector2I(2, 0);

    // Pool Storage
    private TileMapLayer[,] _layers;
    private Vector2I[,] _assignedChunk;

    // Cached sizes
    private int _poolW;
    private int _poolH;

    private int _chunkWpx;
    private int _chunkHpx;
    private int _worldWidthPx;

    public override void _Ready()
    {
        _worldGen = GetNodeOrNull<WorldGenerator>(worldGeneratorPath);
        if (_worldGen == null)
        {
            GD.PushError("ChunkRenderer: worldGeneratorPath is missing / invalid.");
            return;
        }

        _camera = GetNodeOrNull<Camera2D>(cameraPath);
        if (_camera == null)
        {
            _camera = GetViewport().GetCamera2D();
            GD.PushWarning("ChunkRenderer: cameraPath is missing / invalid. Attempting generic camera.");
        }

        if (_camera == null)
        {
            GD.PushError("ChunkRenderer: No camera found. Assign valid cameraPath");
            return;
        }

        if (TileSet == null)
        {
            GD.PushError("ChunkRenderer: TileSet is missing / invalid.");
        }

        Vector2I tileSize = TileSet.TileSize;

        _chunkWpx = WorldConstants.CHUNK_W * tileSize.X;
        _chunkHpx = WorldConstants.CHUNK_H * tileSize.Y;

        _worldWidthPx = _worldGen.WorldWidthTiles * tileSize.X;

        BuildPool();
        ForceFullRefresh();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_worldGen.World == null || _camera == null) return;

        UpdateVisibleWindow();
    }

    private void BuildPool()
    {
        _poolW = (RadiusChunksX * 2) + 1;
        _poolH = (RadiusChunksY * 2) + 1;

        _layers = new TileMapLayer[_poolW, _poolH];
        _assignedChunk = new Vector2I[_poolW, _poolH];

        for (int px = 0; px < _poolW; px++)
            for (int py = 0; py < _poolH; py++)
            {
                TileMapLayer layer = new TileMapLayer
                {
                    TileSet = TileSet
                };

                AddChild(layer);

                _layers[px, py] = layer;
                _assignedChunk[px, py] = new Vector2I(int.MinValue, int.MinValue);
            }
    }

    private void ForceFullRefresh()
    {
        for (int px = 0; px < _poolW; px++)
            for (int py = 0; py < _poolH; py++)
                _assignedChunk[px, py] = new Vector2I(int.MinValue, int.MinValue);
    }

    private void UpdateVisibleWindow()
    {
        // Determine camera center in tile coords, then chunk coords
        Vector2 camPos = _camera.GlobalPosition;

        Vector2I tileSize = TileSet.TileSize;
        int camTileX = Mathf.FloorToInt(camPos.X / tileSize.X);
        int camTileY = Mathf.FloorToInt(camPos.Y / tileSize.Y);

        // Wrap camera tile X because world is finite wrap
        camTileX = _worldGen.World.WrapX(camTileX);

        int camChunkX = camTileX / WorldConstants.CHUNK_W;
        int camChunkY = camTileY / WorldConstants.CHUNK_H;

        // Assign pool slots around the camera center chunk
        for (int oy = -RadiusChunksY; oy <= RadiusChunksY; oy++)
            for (int ox = -RadiusChunksX; ox <= RadiusChunksX; ox++)
            {
                int poolX = ox + RadiusChunksX;
                int poolY = oy + RadiusChunksY;

                int wantChunkX = camChunkX + ox;
                int wantChunkY = camChunkY + oy;

                // Wrap X chunk index (finite circumference)
                wantChunkX = Mathf.PosMod(wantChunkX, _worldGen.World.ChunkCountX);

                // Clamp Y chunk index (no vertical wrap)
                if(wantChunkY < 0 || wantChunkY >= _worldGen.World.ChunkCountY)
                {
                    // Out of bounds: clear and hide this layer
                    _layers[poolX, poolY].Visible = false;
                    _layers[poolX, poolY].Clear();
                    _assignedChunk[poolX, poolY] = new Vector2I(int.MinValue, int.MinValue);
                    continue;                   
                }

                _layers[poolX, poolY].Visible = true;

                Vector2I assigned = _assignedChunk[poolX, poolY];
                bool reassigned = assigned.X != wantChunkX || assigned.Y != wantChunkY;

                ChunkData chunk = _worldGen.World.GetChunk(wantChunkX, wantChunkY);

                // Place the layer at the nearest wrapped image relative to camera X
                float placedX = NearestImageChunkX(wantChunkX, camPos.X);
                float placedY = wantChunkY * _chunkHpx;
                _layers[poolX, poolY].GlobalPosition = new Vector2(placedX, placedY);

                // Render only if needed
                if (reassigned || (chunk != null & chunk.DirtyRender))
                {
                    RenderChunkIntoLayer(_layers[poolX, poolY], wantChunkX, wantChunkY);
                    _assignedChunk[poolX, poolY] = new Vector2I(wantChunkX, wantChunkY);

                    if (chunk != null)
                        chunk.DirtyRender = false;
                }
            }
    }

    private float NearestImageChunkX(int wrappedChunkX, float cameraX)
    {
        float baseX = wrappedChunkX * _chunkWpx;

        // Pick among baseX, baseX - worldWidth, baseX + worldWidth, whichever is closest
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

    private void RenderChunkIntoLayer(TileMapLayer layer, int chunkX, int chunkY)
    {
        layer.Clear();

        int startX = chunkX * WorldConstants.CHUNK_W;
        int startY = chunkY * WorldConstants.CHUNK_H;

        // Render into local chunk space
        for (int ly = 0; ly < WorldConstants.CHUNK_H; ly++)
            for (int lx = 0; lx < WorldConstants.CHUNK_W; lx++)
            {
                int worldX = startX + lx;
                int worldY = startY + ly;

                if (worldY < 0 || worldY >= _worldGen.WorldHeightTiles) continue;
                if (worldX < 0 || worldX >= _worldGen.WorldWidthTiles) continue;

                ushort id = _worldGen.World.GetTerrain(worldX, worldY);
                if (id == TileIds.AIR) continue;
                
                Vector2I atlas = id switch
                {
                    TileIds.DIRT => DirtAtlas,
                    TileIds.STONE => StoneAtlas,
                    TileIds.BEDROCK => BedrockAtlas,
                    _ => DirtAtlas
                };
                
                layer.SetCell(new Vector2I(lx, ly), SourceId, atlas);
            }
    }
}