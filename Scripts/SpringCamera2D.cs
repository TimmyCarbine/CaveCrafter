using Godot;

public partial class SpringCamera2D : Camera2D
{
    [Export] public bool SnapToPlayerOnStart = true;

    [Export] public float SmoothTimeInSeconds = 1.35f;
    [Export] public float MaxFollowSpeed = 20000f;

    [Export] public float StopDistanceEpsilon = 0.25f;
    [Export] public float StopSpeedEpsilon = 2.0f;

    private CharacterBody2D _player;
    private Vector2 _velocity = Vector2.Zero;

    public override void _Ready()
    {
        MakeCurrent();
        TopLevel = true;

        _player = GetParent<CharacterBody2D>();

        if (SnapToPlayerOnStart)
            CallDeferred(nameof(SnapNow));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null) return;

        float dt = (float)delta;

        Vector2 camPos = GlobalPosition;
        Vector2 target = _player.GlobalPosition;

        Vector2 toTarget = target - camPos;

        // If camera is extremely close AND alreay moving very slowly, just stop the camera.
        if (toTarget.Length() <= StopDistanceEpsilon && _velocity.Length() <= StopSpeedEpsilon)
        {
            _velocity = Vector2.Zero;
            return;
        }

        Vector2 nextPos = SmoothDampVector2(
            current: camPos,
            target: target,
            currentVelocity: ref _velocity,
            smoothTime: Mathf.Max(0.0001f, SmoothTimeInSeconds),
            maxSpeed: Mathf.Max(0f, MaxFollowSpeed),
            deltaTime: dt
        );

        GlobalPosition = nextPos;
    }

    private static Vector2 SmoothDampVector2(
        Vector2 current,
        Vector2 target,
        ref Vector2 currentVelocity,
        float smoothTime,
        float maxSpeed,
        float deltaTime)
    {
        smoothTime = Mathf.Max(0.0001f, smoothTime);
        float omega = 2f / smoothTime;

        float x = omega * deltaTime;
        float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

        Vector2 change = current - target;
        Vector2 originalTarget = target;

        float maxChange = maxSpeed * smoothTime;
        float changeLen = change.Length();
        if (changeLen > maxChange && changeLen > 0.0001f)
            change = change / changeLen * maxChange;

        target = current - change;

        Vector2 temp = (currentVelocity + omega * change) * deltaTime;
        currentVelocity = (currentVelocity - omega * temp) * exp;

        Vector2 output = target + (change + temp) * exp;

        Vector2 toOriginal = originalTarget - current;
        Vector2 toOutput = output - originalTarget;

        // Overshoot prevention
        if (toOriginal.Dot(toOutput) > 0f)
        {
            output = originalTarget;
            currentVelocity = Vector2.Zero;
        }
        
        return output;
    }

    public void SnapNow()
    {
        if (_player == null)
        {
            GD.PrintErr("SpringCamera2D SnapNow failed: _player is null. Camera must be a child of Player OR you must set a player path.");
            return;
        }

        GlobalPosition = _player.GlobalPosition;
        _velocity = Vector2.Zero;
        ResetPhysicsInterpolation();
    }
}