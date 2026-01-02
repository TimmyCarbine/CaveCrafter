using Godot;

public partial class BlockEditController : Node
{
    [Export] private NodePath worldGeneratorPath;
    private WorldGenerator _worldGen;

    public override void _Ready()
    {
        _worldGen = GetNodeOrNull<WorldGenerator>(worldGeneratorPath);
        if (_worldGen == null)
            GD.PushError("BlockEditController: worldGeneratorPath is missing / invalid.");
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_worldGen == null) return;

        if (e is not InputEventMouseButton mb || !mb.Pressed) return;

        // Convert mouse to world cell
        Vector2 mouseWorld = GetViewport().GetCamera2D().GetGlobalMousePosition();
        Vector2I cell = _worldGen.WorldToCell(mouseWorld);

        if (mb.ButtonIndex == MouseButton.Left)
            _worldGen.DigCell(cell);
        else if (mb.ButtonIndex == MouseButton.Right)
            _worldGen.PlaceCell(cell, TileIds.DIRT);
    }
}