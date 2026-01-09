using Godot;

public partial class FreeCameraToggle : Node
{
    [Export] private NodePath playerControllerPath;
    [Export] private NodePath springCameraPath;
    [Export] private NodePath freeCameraPath;

    [Export] public float FreeCamSpeedPxPerSec = 1200f;
    [Export] public float FastMultiplier = 2.5f;

    // --- ZOOM SETTINGS ---
    [Export] public float MinZoom = 0.25f;   // Zoomed-in limit
    [Export] public float MaxZoom = 4.0f;    // Zoomed-out limit
    [Export] public float ZoomStep = 0.1f;   // Per wheel notch

    private PlayerController _player;
    private Camera2D _springCam;
    private Camera2D _freeCam;

    private bool _freeCamActive = false;

    public override void _Ready()
    {
        _player = GetNodeOrNull<PlayerController>(playerControllerPath);
        if (_player == null) GD.PushError("FreeCameraToggle: playerControllerPath is missing / invalid.");

        _springCam = GetNodeOrNull<Camera2D>(springCameraPath);
        if (_springCam == null) GD.PushError("FreeCameraToggle: springCameraPath is missing / invalid.");

        _freeCam = GetNodeOrNull<Camera2D>(freeCameraPath);
        if (_freeCam == null) GD.PushError("FreeCameraToggle: freeCameraPath is missing / invalid.");

        // Ensure follow cam starts current (normal gameplay)
        _springCam?.MakeCurrent();

        SetPlayerControlsEnabled(true);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // Toggle freecam
        if (e.IsActionPressed("toggle_free_cam"))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Mouse wheel zoom (freecam only)
        if (_freeCamActive && e is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                ApplyZoom(Input.IsActionPressed("free_cam_fast") ? -ZoomStep * FastMultiplier : -ZoomStep);
                GetViewport().SetInputAsHandled();
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                ApplyZoom(Input.IsActionPressed("free_cam_fast") ? +ZoomStep * FastMultiplier : +ZoomStep);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_freeCamActive || _freeCam == null) return;

        float dt = (float)delta;

        Vector2 move = Vector2.Zero;

        if (Input.IsActionPressed("free_cam_up")) move.Y -= 1f;
        if (Input.IsActionPressed("free_cam_down")) move.Y += 1f;
        if (Input.IsActionPressed("free_cam_left")) move.X -= 1f;
        if (Input.IsActionPressed("free_cam_right")) move.X += 1f;

        if (move.LengthSquared() > 0f)
            move = move.Normalized();

        float speed = FreeCamSpeedPxPerSec;
        if (Input.IsActionPressed("free_cam_fast")) speed *= FastMultiplier;

        _freeCam.GlobalPosition += move * speed * dt;
        _freeCam.ResetPhysicsInterpolation();
    }

    private void Toggle()
    {
        _freeCamActive = !_freeCamActive;

        if (_freeCamActive)
        {
            if (_springCam != null && _freeCam != null)
            {
                // Match position + zoom so there is no visual pop
                _freeCam.GlobalPosition = _springCam.GlobalPosition;
                _freeCam.Zoom = _springCam.Zoom;
            }

            _freeCam?.MakeCurrent();
            SetPlayerControlsEnabled(false);
        }
        else
        {
            _springCam?.MakeCurrent();
            SetPlayerControlsEnabled(true);
            _springCam?.ResetPhysicsInterpolation();
        }
    }

    private void ApplyZoom(float delta)
    {
        if (_freeCam == null) return;

        float current = _freeCam.Zoom.X;
        float target = Mathf.Clamp(current + delta, MinZoom, MaxZoom);

        _freeCam.Zoom = new Vector2(target, target);
    }

    private void SetPlayerControlsEnabled(bool enabled)
    {
        if (_player == null) return;
        _player.SetControlsEnabled(enabled);
    }
}
