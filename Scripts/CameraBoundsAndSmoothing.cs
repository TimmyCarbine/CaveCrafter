using Godot;

/// <summary>
/// Configures Camera2D smoothing + clamps the camera to the world bounds.
/// Attach to the Camera2D node (child of Player).
/// </summary>
public partial class CameraBoundsAndSmoothing : Camera2D
{
    // ----------------------------
    // Tune these in the Inspector
    // ----------------------------
    [Export] private int _worldWidthTiles = 256;
    [Export] private int _worldHeightTiles = 128;

    // How smooth the follow is (higher = snappier, lower = floatier).
    [Export] private float _smoothingSpeed = 9.0f;

    // Optional padding so you can see a little "past" the edge, or keep it tight at 0.
    [Export] private int _paddingPixels = 0;

    public override void _Ready()
    {
        // Enable smooth camera motion (not 1:1).
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = _smoothingSpeed;

        // Compute world size in pixels.
        // For TileMapLayer, your tile size should be 32x32 (or whatever your TileSet uses).
        // If you ever change tile size, update this constant or export it too.
        const int TILE_SIZE = 32;

        int worldWidthPx = _worldWidthTiles * TILE_SIZE;
        int worldHeightPx = _worldHeightTiles * TILE_SIZE;

        // Compute half of the visible viewport in world pixels, accounting for zoom.
        // This prevents the camera from showing outside the world at the edges.
        Vector2 viewportPx = GetViewportRect().Size;
        float halfViewW = (viewportPx.X * 0.5f) * Zoom.X;
        float halfViewH = (viewportPx.Y * 0.5f) * Zoom.Y;

        // Set camera limits in pixels.
        // Limits are in global world space, not tile coords.
        LimitLeft = Mathf.RoundToInt(halfViewW) + _paddingPixels;
        LimitTop = Mathf.RoundToInt(halfViewH) + _paddingPixels;
        LimitRight = worldWidthPx - Mathf.RoundToInt(halfViewW) - _paddingPixels;
        LimitBottom = worldHeightPx - Mathf.RoundToInt(halfViewH) - _paddingPixels;
    }
}
