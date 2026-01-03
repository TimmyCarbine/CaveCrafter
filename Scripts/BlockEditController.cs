using Godot;

public partial class BlockEditController : Node
{
    [Export] private NodePath worldGeneratorPath;
    [Export] private NodePath chunkRendererPath;
    [Export] private NodePath cameraPath;

    private WorldGenerator _gen;
    private ChunkRenderer _renderer;
    private Camera2D _camera;

    public override void _Ready()
    {
        _gen = GetNodeOrNull<WorldGenerator>(worldGeneratorPath);
        if (_gen == null)
            GD.PushError("BlockEditController: worldGeneratorPath is missing / invalid.");

        _renderer = GetNodeOrNull<ChunkRenderer>(chunkRendererPath);
        if (_renderer == null)
            GD.PushError("BlockEditController: chunkRendererPath is missing / invalid.");

        _camera = GetNodeOrNull<Camera2D>(cameraPath);
        if (_camera == null)
        {
            _camera = GetViewport().GetCamera2D();
            GD.PushWarning("BlockEditController: cameraPath is missing / invalid. Attempting generic camera.");
        }

        if (_camera == null)
        {
            GD.PushError("BlockEditController: No camera found. Assign valid cameraPath");
            return;
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_gen == null) return;

        if (e is not InputEventMouseButton mb || !mb.Pressed) return;

        Vector2I tileSize = _renderer.TileSet.TileSize;

        // Convert mouse to world cell
        Vector2 mouseWorld = _camera.GetGlobalMousePosition();

        int cx = Mathf.FloorToInt(mouseWorld.X / tileSize.X);
        int cy = Mathf.FloorToInt(mouseWorld.Y / tileSize.Y);

        if (mb.ButtonIndex == MouseButton.Left && _gen.World.GetTerrain(cx, cy) != TileIds.BEDROCK)
            _gen.World.Dig(cx, cy);
        else if (mb.ButtonIndex == MouseButton.Right && _gen.World.GetTerrain(cx, cy) == TileIds.AIR)
            _gen.World.Place(cx, cy, TileIds.DIRT);
    }
}