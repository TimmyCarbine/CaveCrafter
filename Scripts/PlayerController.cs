using Godot;

/// <summary>
/// Basic 2D platformer movement for a CharacterBody2D.
/// Uses Input Map actions: move_left, move_right, jump.
/// </summary>
public partial class PlayerController : CharacterBody2D
{
    // ----------------------------
    // Tunables
    // ----------------------------
    private const float MOVE_SPEED = 220.0f;
    private const float JUMP_VELOCITY = -420.0f;

    // Godot provides default project gravity, but we read it so it's consistent.
    private float _gravity;

    public override void _Ready()
    {
        // Grab the project gravity setting (Project Settings → Physics → 2D).
        _gravity = (float)ProjectSettings.GetSetting("physics/2d/default_gravity");
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocity = Velocity;

        // Apply gravity when not grounded.
        if (!IsOnFloor())
        {
            velocity.Y += _gravity * (float)delta;
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
    }
}
