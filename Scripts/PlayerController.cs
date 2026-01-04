using Godot;

/// <summary>
/// Basic 2D platformer movement for a CharacterBody2D.
/// Uses Input Map actions: move_left, move_right, jump.
/// </summary>
public partial class PlayerController : CharacterBody2D
{
    // Tuning
    [Export] public float WorldOriginX = 0f;
    [Export] public float WrapMarginPx = 2f;        // how far past edge before wrapping
    [Export] public float WrapCooldownSeconds = 0.1f; // prevents immediate rewrap loops
    [Export] public NodePath springCameraPath;
    [Export] public NodePath worldGeneratorPath;
    [Export] public NodePath chunkRendererPath;

    private float WorldWidthPx;
    private float _wrapCooldown = 0f;
    private SpringCamera2D _springCam;
    private WorldGenerator _gen;
    private ChunkRenderer _renderer;
    private bool _controlsEnabled = true;

    private const float MOVE_SPEED = 220.0f;
    private const float JUMP_VELOCITY = -420.0f;

    // Godot provides default project gravity, but we read it so it's consistent.
    private float _gravity;

    public override void _Ready()
    {
        // Grab the project gravity setting (Project Settings → Physics → 2D).
        _gravity = (float)ProjectSettings.GetSetting("physics/2d/default_gravity");

        _springCam = GetNodeOrNull<SpringCamera2D>(springCameraPath);
        if (_springCam == null) GD.PushError("PlayerController: springCameraPath is missing / invalid.");

        _gen = GetNodeOrNull<WorldGenerator>(worldGeneratorPath);
        _renderer = GetNodeOrNull<ChunkRenderer>(chunkRendererPath);
        if (_gen == null)
        {
            GD.PushError("PlayerController: worldGeneratorPath is missing / invalid.");
        }
        else if (_renderer.TileSet == null)
        {
            GD.PushError("PlayerController: chunkRendererPath or TileSet is missing / invalid.");
        }  
        else
        {
            WorldWidthPx = _gen.WorldWidthTiles * _renderer.TileSet.TileSize.X;
            GD.Print($"PlayerController: WorldWidthPx: {WorldWidthPx}px");
        }

    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Vector2 velocity = Velocity;

        // Apply gravity when not grounded.
        if (!IsOnFloor())
        {
            velocity.Y += _gravity * (float)delta;
        }

        if (!_controlsEnabled)
        {
            MoveAndSlide();
            WrapPlayerX((float)delta);
            return;
        }

        // Horizontal input: -1 (left), 0, +1 (right)
        float input = 0.0f;

        if (Input.IsActionPressed("move_left"))
            input -= 1.0f;

        if (Input.IsActionPressed("move_right"))
            input += 1.0f;

        velocity.X = input * MOVE_SPEED;

        // Jump only when on the floor.
        if (IsOnFloor() && Input.IsActionJustPressed("jump"))
        {
            velocity.Y = JUMP_VELOCITY;
        }

        Velocity = velocity;

        // Move and collide against physics bodies.
        MoveAndSlide();
        WrapPlayerX(dt);
    }

    private void WrapPlayerX(float dt)
    {
        if (_wrapCooldown > 0f)
        {
            _wrapCooldown -= dt;
            return;
        }

        float minX = WorldOriginX;
        float maxX = WorldOriginX + WorldWidthPx;

        float x = GlobalPosition.X;

        // Wrap only when clearly outside
        if (x < minX - WrapMarginPx)
        {
            float dx = WorldWidthPx;
            GlobalPosition += new Vector2(dx, 0f);
            ResetPhysicsInterpolation();

            _springCam?.OnPlayerWrapped(dx);

            _wrapCooldown = WrapCooldownSeconds;
        }
        else if (x > maxX + WrapMarginPx)
        {
            float dx = -WorldWidthPx;
            GlobalPosition += new Vector2(dx, 0f);
            ResetPhysicsInterpolation();

            _springCam?.OnPlayerWrapped(dx);

            _wrapCooldown = WrapCooldownSeconds;
        }
    }

    public void SetControlsEnabled(bool enabled)
    {
        _controlsEnabled = enabled;

        if (!enabled) Velocity = new Vector2(0f, Velocity.Y);
    }
}
