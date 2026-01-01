using Godot;

/// <summary>
/// Critically-damped spring camera:
/// - No deadzone; camera target is always the player (plus optional lookahead).
/// - Speed naturally increases with distance (so falls stay on screen).
/// - Uses SmoothDamp (critically damped) so it does NOT wobble around the player.
/// - World bounds from TileMap used rect (recommended) with vertical padding.
/// - World-edge braking to avoid sliding into void.
/// </summary>
public partial class SpringCamera2DOriginal : Camera2D
{
    // Follow Feel
    [Export] private string SmoothTimeSecondsTooltip = 
    "Time (seconds) for the camera to critically damp toward the target. Lower = snappier, Higher = floatier.";
    [Export] public float SmoothTimeSeconds = 1.35f;

    [Export] private string MaxFollowSpeedTooltip = 
    "Hard cap to avoid insane speeds. Increase if you want faster falling catch-up.";
    [Export] public float MaxFollowSpeed = 2200f;

    [Export] private string StopDistanceEpsilonTooltip = 
    "When very close to target, snap velocity to zero to prevent micro-jitter. (world px)";
    [Export] public float StopDistanceEpsilon = 0.25f; 
    [Export] private string StopSpeedEpsilonTooltip = 
    "When very close to target, snap velocity to zero to prevent micro-jitter. (world px/sec)";
    [Export] public float StopSpeedEpsilon = 2.0f;

    // Optional: Look ahead (screen px)
    [Export] private string LookAheadXPxTooltip = 
    "Horizontal look ahead in screen px";
    [Export] public float LookAheadXPx = 0f;
    [Export] private string LookAheadYPxTooltip = 
    "Vertical look ahead in screen px";
    [Export] public float LookAheadYPx = 0f;

    // World Bounds (used-rect)
    [Export] public bool UseTileMapUsedRectBounds = true;
    [Export] public NodePath BoundsTileMapLayerPath;
    [Export] public int UsedRectTopPaddingTiles = 40;
    [Export] public int UsedRectBottomPaddingTiles = 40;

    // World Bounds (fallback)
    [Export] public int WorldWidthTiles = 256;
    [Export] public int WorldHeightTiles = 128;
    [Export] public int TileSizePx = 32;
    [Export] public Vector2 WorldOriginPx = Vector2.Zero;

    // World Edge Braking (world px)
    [Export] private string WorldEdgeBrakingDistancePxTooltip = 
    "As we approach bounds, we slow down so we settle before the edge.";
    [Export] public float WorldEdgeBrakeDistancePx = 1200f;

    // Startup / Debug
    [Export] public bool SnapToPlayerOnStart = true;
    [Export] public bool ShowDebug = false;

    // ----------------------------
    // Internal
    // ----------------------------
    private CharacterBody2D _player;

    // SmoothDamp velocity accumulator (world px/sec).
    private Vector2 _velocity = Vector2.Zero;

    // Cached camera-centre bounds (already includes half viewport extents).
    private float _minX, _maxX, _minY, _maxY;
    private bool _boundsReady = false;

    private TileMapLayer _boundsLayer;

    public override void _Ready()
    {
        MakeCurrent();
        TopLevel = true;

        _player = GetParent<CharacterBody2D>();

        if (UseTileMapUsedRectBounds && BoundsTileMapLayerPath != null && !BoundsTileMapLayerPath.IsEmpty)
            _boundsLayer = GetNodeOrNull<TileMapLayer>(BoundsTileMapLayerPath);

        CallDeferred(nameof(RebuildWorldBounds));

        if (SnapToPlayerOnStart && _player != null)
        {
            // Snap immediately; clamp after bounds are ready.
            GlobalPosition = _player.GlobalPosition;
            _velocity = Vector2.Zero;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null)
            return;

        if (ShowDebug)
            QueueRedraw();

        float dt = (float)delta;

        if (!_boundsReady)
            RebuildWorldBounds();

        Vector2 camPos = GlobalPosition;

        // Look-ahead in WORLD px (screen px -> world px = / Zoom).
        Vector2 lookAheadWorld = new Vector2(LookAheadXPx / Zoom.X, LookAheadYPx / Zoom.Y);
        Vector2 desiredTarget = _player.GlobalPosition + lookAheadWorld;

        // Clamp the target to world bounds so we never "want" to go into void.
        if (_boundsReady)
            desiredTarget = ClampToWorld(desiredTarget);

        // Critically damped move toward target (no oscillation).
        Vector2 nextPos = SmoothDampVector2(
            current: camPos,
            target: desiredTarget,
            currentVelocity: ref _velocity,
            smoothTime: Mathf.Max(0.001f, SmoothTimeSeconds),
            maxSpeed: Mathf.Max(0f, MaxFollowSpeed),
            deltaTime: dt
        );

        // Apply world-edge braking by scaling velocity near edges (prevents drifting into clamp).
        if (_boundsReady && WorldEdgeBrakeDistancePx > 0.01f)
            ApplyEdgeBraking(ref nextPos, ref _velocity, camPos);

        // Clamp final position (authoritative).
        if (_boundsReady)
        {
            Vector2 clamped = ClampToWorld(nextPos);

            // If clamped, kill velocity into the wall.
            if (!Mathf.IsEqualApprox(clamped.X, nextPos.X)) _velocity.X = 0f;
            if (!Mathf.IsEqualApprox(clamped.Y, nextPos.Y)) _velocity.Y = 0f;

            nextPos = clamped;
        }

        // Stop tiny micro motion near target.
        float dist = (desiredTarget - nextPos).Length();
        if (dist <= StopDistanceEpsilon && _velocity.Length() <= StopSpeedEpsilon)
            _velocity = Vector2.Zero;

        GlobalPosition = nextPos;
    }

    // ------------------------------------------------------------
    // Bounds
    // ------------------------------------------------------------
    public void RebuildWorldBounds()
    {
        if (!UseTileMapUsedRectBounds || _boundsLayer == null || _boundsLayer.TileSet == null)
        {
            GetManualBounds(out _minX, out _maxX, out _minY, out _maxY);
            _boundsReady = true;

            if (SnapToPlayerOnStart && _player != null)
                GlobalPosition = ClampToWorld(_player.GlobalPosition);

            return;
        }

        Rect2I used = _boundsLayer.GetUsedRect();
        if (used.Size.X <= 0 || used.Size.Y <= 0)
        {
            _boundsReady = false;
            CallDeferred(nameof(RebuildWorldBounds));
            return;
        }

        Vector2 tileSize = _boundsLayer.TileSet.TileSize;

        // Calibrate MapToLocal centre vs origin (Godot can vary here).
        Vector2 m00 = _boundsLayer.MapToLocal(Vector2I.Zero);
        bool mapToLocalIsCentre =
            Mathf.IsEqualApprox(m00.X, tileSize.X * 0.5f) &&
            Mathf.IsEqualApprox(m00.Y, tileSize.Y * 0.5f);

        Vector2 CellToWorld(Vector2I cell)
        {
            Vector2 local = _boundsLayer.MapToLocal(cell);
            if (mapToLocalIsCentre)
                local -= tileSize * 0.5f; // centre -> origin (top-left)

            return _boundsLayer.ToGlobal(local);
        }

        Vector2I start = used.Position;
        Vector2I endExclusive = used.Position + used.Size;

        // Add vertical padding for sky/depth.
        start.Y -= UsedRectTopPaddingTiles;
        endExclusive.Y += UsedRectBottomPaddingTiles;

        float leftEdge = CellToWorld(start).X;
        float topEdge = CellToWorld(start).Y;

        float rightEdge = CellToWorld(endExclusive).X;
        float bottomEdge = CellToWorld(endExclusive).Y;

        // Viewport extents are / Zoom (NOT * Zoom)
        Vector2 view = GetViewportRect().Size;
        float halfViewW = (view.X * 0.5f) / Zoom.X;
        float halfViewH = (view.Y * 0.5f) / Zoom.Y;

        _minX = leftEdge + halfViewW;
        _maxX = rightEdge - halfViewW;
        _minY = topEdge + halfViewH;
        _maxY = bottomEdge - halfViewH;

        if (_minX > _maxX) _minX = _maxX = (leftEdge + rightEdge) * 0.5f;
        if (_minY > _maxY) _minY = _maxY = (topEdge + bottomEdge) * 0.5f;

        _boundsReady = true;

        if (SnapToPlayerOnStart && _player != null)
        {
            GlobalPosition = ClampToWorld(_player.GlobalPosition);
            _velocity = Vector2.Zero;
        }
    }

    private void GetManualBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        float worldW = WorldWidthTiles * TileSizePx;
        float worldH = WorldHeightTiles * TileSizePx;

        Vector2 view = GetViewportRect().Size;
        float halfViewW = (view.X * 0.5f) / Zoom.X;
        float halfViewH = (view.Y * 0.5f) / Zoom.Y;

        minX = WorldOriginPx.X + halfViewW;
        maxX = WorldOriginPx.X + worldW - halfViewW;
        minY = WorldOriginPx.Y + halfViewH;
        maxY = WorldOriginPx.Y + worldH - halfViewH;
    }

    private Vector2 ClampToWorld(Vector2 pos)
    {
        pos.X = Mathf.Clamp(pos.X, _minX, _maxX);
        pos.Y = Mathf.Clamp(pos.Y, _minY, _maxY);
        return pos;
    }

    // ------------------------------------------------------------
    // Edge braking
    // ------------------------------------------------------------
    private void ApplyEdgeBraking(ref Vector2 nextPos, ref Vector2 velocity, Vector2 currentPos)
    {
        float brakeX = 1f;
        float brakeY = 1f;

        // Use current position to decide “how close are we to the edge in the direction we’re moving?”
        if (velocity.X > 0f)
            brakeX = Mathf.Clamp((_maxX - currentPos.X) / WorldEdgeBrakeDistancePx, 0f, 1f);
        else if (velocity.X < 0f)
            brakeX = Mathf.Clamp((currentPos.X - _minX) / WorldEdgeBrakeDistancePx, 0f, 1f);

        if (velocity.Y > 0f)
            brakeY = Mathf.Clamp((_maxY - currentPos.Y) / WorldEdgeBrakeDistancePx, 0f, 1f);
        else if (velocity.Y < 0f)
            brakeY = Mathf.Clamp((currentPos.Y - _minY) / WorldEdgeBrakeDistancePx, 0f, 1f);

        // Scale velocity (this is the important part; it removes “pressing into clamp” drift).
        velocity.X *= brakeX;
        velocity.Y *= brakeY;

        // Recompute nextPos using the braked velocity to keep it consistent.
        // (Otherwise you can still "step" too far then clamp.)
        nextPos = currentPos + velocity * (float)GetPhysicsProcessDeltaTime();
    }

    // ------------------------------------------------------------
    // Critically damped smoothing (Unity-style SmoothDamp)
    // ------------------------------------------------------------
    private static Vector2 SmoothDampVector2(
        Vector2 current,
        Vector2 target,
        ref Vector2 currentVelocity,
        float smoothTime,
        float maxSpeed,
        float deltaTime)
    {
        // Based on Game Programming Gems / Unity SmoothDamp
        smoothTime = Mathf.Max(0.0001f, smoothTime);
        float omega = 2f / smoothTime;

        float x = omega * deltaTime;
        float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

        Vector2 change = current - target;
        Vector2 originalTarget = target;

        // Clamp maximum speed (limits how far we can move in smoothTime).
        float maxChange = maxSpeed * smoothTime;
        float changeLen = change.Length();
        if (changeLen > maxChange && changeLen > 0.0001f)
            change = change / changeLen * maxChange;

        target = current - change;

        Vector2 temp = (currentVelocity + omega * change) * deltaTime;
        currentVelocity = (currentVelocity - omega * temp) * exp;

        Vector2 output = target + (change + temp) * exp;

        // Prevent overshoot (critical for your wobble issue).
        Vector2 toOriginal = originalTarget - current;
        Vector2 toOutput = output - originalTarget;
        if (toOriginal.Dot(toOutput) > 0f)
        {
            output = originalTarget;
            currentVelocity = (output - originalTarget) / Mathf.Max(0.0001f, deltaTime); // effectively zero
        }

        return output;
    }

    public override void _Draw()
    {
        if (!ShowDebug || _player == null)
            return;

        Vector2 localPlayer = ToLocal(_player.GlobalPosition);
        DrawCircle(localPlayer, 6, Colors.Red);
        DrawLine(Vector2.Zero, localPlayer, Colors.Red, 1);

        DrawLine(Vector2.Zero, _velocity * 0.1f, Colors.Green, 3);
    }
}
