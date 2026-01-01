using Godot;

/// <summary>
/// Deadzone camera:
/// - Hard deadzone freeze while player stays inside.
/// - X edge-lock + recentre boost to gently pull player back toward centre.
/// - Rubber-band acceleration / deceleration (camera has mass).
/// - Distance-based accel boost (direction changes feel responsive).
/// - World bounds from TileMapLayer used rect (recommended) with optional Y padding tiles for sky/depth.
/// - World-edge braking (slows before bounds, never shows void).
/// - Full Y axis support (deadzone follow + rubber band + clamp).
///
/// Attach to Camera2D (recommended as a child of Player).
/// IMPORTANT: TopLevel = true prevents inheriting parent transform (prevents jitter).
/// </summary>
public partial class DeadzoneCamera2D : Camera2D
{
    // ----------------------------
    // Deadzone (SCREEN pixels)
    // ----------------------------
    [Export] public float DeadzoneWidthPx = 800f;
    [Export] public float DeadzoneHeightPx = 400f;

    // ----------------------------
    // X-only edge detection (SCREEN pixels)
    // ----------------------------
    [Export] public float EdgeEnterBandPx = 200f;
    [Export] public float EdgeExitBandPx = 400f;

    // ----------------------------
    // Camera speed caps (WORLD px/sec)
    // ----------------------------
    [Export] public float BaseFollowSpeed = 200f;
    [Export] public float BoostSpeedMultiplier = 1.2f;

    // ----------------------------
    // Rubber-band feel
    // ----------------------------
    [Export] public float AccelDecelTimeSeconds = 3.0f;
    [Export] public float StopSpeedEpsilon = 2.0f;

    // ----------------------------
    // Distance accel boost (helps fast direction reversals)
    // ----------------------------
    [Export] public float DistanceAccelMultiplier = 3.0f;
    [Export] public float DistanceAccelExponent = 1.5f;

    // ----------------------------
    // Recentre behaviour (X only)
    // ----------------------------
    [Export] public float PushToRecentreTime = 0.25f;
    [Export] public float RecentreRampSpeed = 6f;
    [Export] public float MinMoveSpeed = 20f;
    [Export] public float DesiredPlayerOffsetXPx = 0f;

    // ----------------------------
    // Linger timers
    // ----------------------------
    [Export] public float EdgeLockLingerTime = 0.20f;
    [Export] public float InsideDeadzoneLingerTime = 0.25f;

    // ----------------------------
    // World bounds (fallback/manual)
    // ----------------------------
    [Export] public int WorldWidthTiles = 256;
    [Export] public int WorldHeightTiles = 128;
    [Export] public int TileSizePx = 32;
    [Export] public Vector2 WorldOriginPx = Vector2.Zero;

    // ----------------------------
    // World bounds (recommended): TileMap used rect
    // ----------------------------
    [Export] public bool UseTileMapUsedRectBounds = true;
    [Export] public NodePath BoundsTileMapLayerPath;

    // IMPORTANT: Add vertical padding so the camera can see sky above the highest placed ground,
    // and deeper below the lowest placed ground, without ever showing void.
    [Export] public int UsedRectTopPaddingTiles = 30;
    [Export] public int UsedRectBottomPaddingTiles = 30;

    // ----------------------------
    // World-edge braking (WORLD pixels)
    // ----------------------------
    [Export] public float WorldEdgeBrakeDistancePx = 1200f;

    // ----------------------------
    // Startup / Debug
    // ----------------------------
    [Export] public bool SnapToPlayerOnStart = true;
    [Export] public bool ShowDebug = false;

    // ----------------------------
    // Internal
    // ----------------------------
    private CharacterBody2D _player;

    private float _recentre01 = 0f;
    private float _pushTimer = 0f;

    // X edge lock dir: -1 left, +1 right, 0 none
    private int _edgeLockDir = 0;

    private float _edgeLockLinger = 0f;
    private float _insideLinger = 0f;

    private Vector2 _camVelocity = Vector2.Zero;

    private bool _didInitialSnap = false;

    // Cached camera-centre bounds (already includes viewport half extents)
    private float _boundMinX, _boundMaxX, _boundMinY, _boundMaxY;
    private bool _boundsReady = false;

    private TileMapLayer _boundsLayer;

    public override void _Ready()
    {
        MakeCurrent();
        TopLevel = true;

        _player = GetParent<CharacterBody2D>();

        if (UseTileMapUsedRectBounds && BoundsTileMapLayerPath != null && !BoundsTileMapLayerPath.IsEmpty)
            _boundsLayer = GetNodeOrNull<TileMapLayer>(BoundsTileMapLayerPath);

        // Build bounds after world gen has likely placed tiles.
        CallDeferred(nameof(RebuildWorldBounds));

        if (SnapToPlayerOnStart)
            CallDeferred(nameof(ForceSnapToPlayerNow));
    }

    private void ForceSnapToPlayerNow()
    {
        if (_player == null)
            return;

        // Ensure bounds are ready BEFORE we clamp the snap.
        if (!_boundsReady)
            RebuildWorldBounds();

        // If bounds still aren't ready (world gen not done), snap raw for now,
        // then we'll clamp once bounds become valid.
        GlobalPosition = _boundsReady ? ClampToWorld(_player.GlobalPosition) : _player.GlobalPosition;

        // Reset motion state.
        _camVelocity = Vector2.Zero;
        _recentre01 = 0f;
        _pushTimer = 0f;
        _edgeLockDir = 0;
        _edgeLockLinger = 0f;
        _insideLinger = 0f;

        _didInitialSnap = true;
    }

    public void RebuildWorldBounds()
    {
        // Fallback/manual bounds if not using used-rect.
        if (!UseTileMapUsedRectBounds || _boundsLayer == null || _boundsLayer.TileSet == null)
        {
            GetWorldBoundsFromManual(out _boundMinX, out _boundMaxX, out _boundMinY, out _boundMaxY);
            _boundsReady = true;
            return;
        }

        Rect2I used = _boundsLayer.GetUsedRect();

        // If not ready yet, try again next frame.
        if (used.Size.X <= 0 || used.Size.Y <= 0)
        {
            _boundsReady = false;
            CallDeferred(nameof(RebuildWorldBounds));
            return;
        }

        Vector2 tileSize = _boundsLayer.TileSet.TileSize;

        // Calibrate MapToLocal (centre vs origin)
        Vector2 m00 = _boundsLayer.MapToLocal(Vector2I.Zero);
        bool mapToLocalIsCentre =
            Mathf.IsEqualApprox(m00.X, tileSize.X * 0.5f) &&
            Mathf.IsEqualApprox(m00.Y, tileSize.Y * 0.5f);

        Vector2 CellToWorld(Vector2I cell)
        {
            Vector2 local = _boundsLayer.MapToLocal(cell);

            // Convert centre -> origin (top-left) if needed.
            if (mapToLocalIsCentre)
                local -= tileSize * 0.5f;

            return _boundsLayer.ToGlobal(local);
        }

        // Expand Y used rect with padding tiles (sky + depth).
        Vector2I start = used.Position;
        Vector2I endExclusive = used.Position + used.Size;

        start.Y -= UsedRectTopPaddingTiles;
        endExclusive.Y += UsedRectBottomPaddingTiles;

        // Compute WORLD edges (tile origin based)
        float leftEdge = CellToWorld(start).X;
        float topEdge = CellToWorld(start).Y;

        float rightEdge = CellToWorld(endExclusive).X;
        float bottomEdge = CellToWorld(endExclusive).Y;

        // Convert to camera-centre bounds with correct zoom math.
        Vector2 view = GetViewportRect().Size;
        float halfViewW = (view.X * 0.5f) / Zoom.X;
        float halfViewH = (view.Y * 0.5f) / Zoom.Y;

        _boundMinX = leftEdge + halfViewW;
        _boundMaxX = rightEdge - halfViewW;
        _boundMinY = topEdge + halfViewH;
        _boundMaxY = bottomEdge - halfViewH;

        // Collapse if world smaller than viewport.
        if (_boundMinX > _boundMaxX) _boundMinX = _boundMaxX = (leftEdge + rightEdge) * 0.5f;
        if (_boundMinY > _boundMaxY) _boundMinY = _boundMaxY = (topEdge + bottomEdge) * 0.5f;

        _boundsReady = true;

        // If we snapped before bounds existed, clamp now so we don't start "below".
        if (SnapToPlayerOnStart && _didInitialSnap)
            GlobalPosition = ClampToWorld(GlobalPosition);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null)
            return;

        if (ShowDebug)
            QueueRedraw();

        if (SnapToPlayerOnStart && !_didInitialSnap)
            ForceSnapToPlayerNow();

        if (!_boundsReady)
            RebuildWorldBounds();

        float dt = (float)delta;

        Vector2 camPos = GlobalPosition;
        Vector2 playerPos = _player.GlobalPosition;

        // SCREEN px -> WORLD px (correct zoom conversion)
        float halfW = (DeadzoneWidthPx / Zoom.X) * 0.5f;
        float halfH = (DeadzoneHeightPx / Zoom.Y) * 0.5f;

        Vector2 offset = playerPos - camPos;

        bool insideX = offset.X >= -halfW && offset.X <= halfW;
        bool insideY = offset.Y >= -halfH && offset.Y <= halfH;

        float vx = _player.Velocity.X;
        bool movingIntent = Mathf.Abs(vx) > MinMoveSpeed;

        // ----------------------------
        // X edge lock (hysteresis + linger)
        // ----------------------------
        float enterBand = EdgeEnterBandPx / Zoom.X;
        float exitBand = EdgeExitBandPx / Zoom.X;

        float distToRightEdge = halfW - offset.X;
        float distToLeftEdge = halfW + offset.X;

        bool nearRightEnter = distToRightEdge <= enterBand;
        bool nearLeftEnter = distToLeftEdge <= enterBand;
        bool nearRightExit = distToRightEdge <= exitBand;
        bool nearLeftExit = distToLeftEdge <= exitBand;

        if (_edgeLockDir == 0 && movingIntent)
        {
            if (vx > MinMoveSpeed && nearRightEnter)
            {
                _edgeLockDir = 1;
                _edgeLockLinger = 0f;
            }
            else if (vx < -MinMoveSpeed && nearLeftEnter)
            {
                _edgeLockDir = -1;
                _edgeLockLinger = 0f;
            }
        }

        if (_edgeLockDir == 1)
        {
            bool shouldRelease = (vx <= MinMoveSpeed) || (!nearRightExit);
            if (shouldRelease)
            {
                _edgeLockLinger += dt;
                if (_edgeLockLinger >= EdgeLockLingerTime)
                {
                    _edgeLockDir = 0;
                    _edgeLockLinger = 0f;
                }
            }
            else _edgeLockLinger = 0f;
        }
        else if (_edgeLockDir == -1)
        {
            bool shouldRelease = (vx >= -MinMoveSpeed) || (!nearLeftExit);
            if (shouldRelease)
            {
                _edgeLockLinger += dt;
                if (_edgeLockLinger >= EdgeLockLingerTime)
                {
                    _edgeLockDir = 0;
                    _edgeLockLinger = 0f;
                }
            }
            else _edgeLockLinger = 0f;
        }

        bool pushingEdgeX = _edgeLockDir != 0;

        // ----------------------------
        // Freeze if fully inside (with linger)
        // ----------------------------
        bool shouldFreeze = insideX && insideY && !pushingEdgeX;

        if (shouldFreeze)
        {
            _insideLinger += dt;

            _pushTimer = Mathf.Max(0f, _pushTimer - dt * 0.5f);
            _recentre01 = Mathf.Max(0f, _recentre01 - dt * (RecentreRampSpeed * 0.5f));

            if (_insideLinger >= InsideDeadzoneLingerTime)
            {
                ApplyRubberBandStop(dt);
                return;
            }
        }
        else
        {
            _insideLinger = 0f;
        }

        // ----------------------------
        // Edge-follow target (X + Y)
        // ----------------------------
        Vector2 edgeTarget = camPos;

        if (!insideX)
        {
            if (offset.X > halfW) edgeTarget.X = playerPos.X - halfW;
            else if (offset.X < -halfW) edgeTarget.X = playerPos.X + halfW;
        }

        if (!insideY)
        {
            if (offset.Y > halfH) edgeTarget.Y = playerPos.Y - halfH;
            else if (offset.Y < -halfH) edgeTarget.Y = playerPos.Y + halfH;
        }

        // ----------------------------
        // Recentre (X only)
        // ----------------------------
        if (pushingEdgeX) _pushTimer += dt;
        else _pushTimer = Mathf.Max(0f, _pushTimer - dt * 2f);

        float desiredRecentre01 = (PushToRecentreTime <= 0.001f)
            ? 1f
            : Mathf.Clamp(_pushTimer / PushToRecentreTime, 0f, 1f);

        float tRecentre = 1f - Mathf.Exp(-RecentreRampSpeed * dt);
        _recentre01 = Mathf.Lerp(_recentre01, desiredRecentre01, tRecentre);

        float desiredOffsetWorldX = DesiredPlayerOffsetXPx / Zoom.X;
        float centreTargetX = playerPos.X - desiredOffsetWorldX;

        Vector2 finalTarget = edgeTarget;
        finalTarget.X = Mathf.Lerp(edgeTarget.X, centreTargetX, _recentre01);

        // Y uses edge-follow only (stable and predictable)
        // If you ever want vertical recentre too, we can add a separate recentre ramp for Y.
        finalTarget.Y = edgeTarget.Y;

        // Don't chase outside bounds.
        finalTarget = ClampToWorld(finalTarget);

        // ----------------------------
        // Distance-based accel boost (X + Y)
        // ----------------------------
        float distNormX = (halfW <= 0.001f) ? 0f : Mathf.Clamp(Mathf.Abs(offset.X) / halfW, 0f, 1f);
        float distNormY = (halfH <= 0.001f) ? 0f : Mathf.Clamp(Mathf.Abs(offset.Y) / halfH, 0f, 1f);

        float distanceFactorX = Mathf.Pow(distNormX, DistanceAccelExponent);
        float distanceFactorY = Mathf.Pow(distNormY, DistanceAccelExponent);

        // ----------------------------
        // Desired speed caps
        // ----------------------------
        float desiredSpeedX = BaseFollowSpeed;
        if (_recentre01 > 0f)
            desiredSpeedX = Mathf.Max(BaseFollowSpeed, Mathf.Abs(vx) * BoostSpeedMultiplier);

        float desiredSpeedY = BaseFollowSpeed;

        float baseAccelX = desiredSpeedX / Mathf.Max(0.001f, AccelDecelTimeSeconds);
        float baseAccelY = desiredSpeedY / Mathf.Max(0.001f, AccelDecelTimeSeconds);

        float accelRateX = baseAccelX * Mathf.Lerp(1f, DistanceAccelMultiplier, distanceFactorX);
        float accelRateY = baseAccelY * Mathf.Lerp(1f, DistanceAccelMultiplier, distanceFactorY);

        // ----------------------------
        // World-edge braking (X + Y)
        // ----------------------------
        GetWorldBounds(out float minX, out float maxX, out float minY, out float maxY);

        float brakeX = 1f;
        if (WorldEdgeBrakeDistancePx > 0.01f)
        {
            if (finalTarget.X > camPos.X || _camVelocity.X > 0f)
                brakeX = Mathf.Clamp((maxX - camPos.X) / WorldEdgeBrakeDistancePx, 0f, 1f);
            else if (finalTarget.X < camPos.X || _camVelocity.X < 0f)
                brakeX = Mathf.Clamp((camPos.X - minX) / WorldEdgeBrakeDistancePx, 0f, 1f);
        }

        float brakeY = 1f;
        if (WorldEdgeBrakeDistancePx > 0.01f)
        {
            if (finalTarget.Y > camPos.Y || _camVelocity.Y > 0f)
                brakeY = Mathf.Clamp((maxY - camPos.Y) / WorldEdgeBrakeDistancePx, 0f, 1f);
            else if (finalTarget.Y < camPos.Y || _camVelocity.Y < 0f)
                brakeY = Mathf.Clamp((camPos.Y - minY) / WorldEdgeBrakeDistancePx, 0f, 1f);
        }

        desiredSpeedX *= brakeX;
        desiredSpeedY *= brakeY;

        accelRateX *= Mathf.Lerp(0.25f, 1f, brakeX);
        accelRateY *= Mathf.Lerp(0.25f, 1f, brakeY);

        // ----------------------------
        // Desired velocities toward target (X + Y)
        // ----------------------------
        Vector2 toTarget = finalTarget - camPos;

        float desiredVelX = Mathf.Abs(toTarget.X) > 0.01f ? Mathf.Sign(toTarget.X) * desiredSpeedX : 0f;
        float desiredVelY = Mathf.Abs(toTarget.Y) > 0.01f ? Mathf.Sign(toTarget.Y) * desiredSpeedY : 0f;

        // Forbid outward velocity at world edges
        const float EDGE_EPS = 0.5f;

        bool atLeft = camPos.X <= minX + EDGE_EPS;
        bool atRight = camPos.X >= maxX - EDGE_EPS;
        if (atLeft && desiredVelX < 0f) desiredVelX = 0f;
        if (atRight && desiredVelX > 0f) desiredVelX = 0f;

        bool atTop = camPos.Y <= minY + EDGE_EPS;
        bool atBottom = camPos.Y >= maxY - EDGE_EPS;
        if (atTop && desiredVelY < 0f) desiredVelY = 0f;
        if (atBottom && desiredVelY > 0f) desiredVelY = 0f;

        // Accelerate toward desired velocities
        _camVelocity.X = Mathf.MoveToward(_camVelocity.X, desiredVelX, accelRateX * dt);
        _camVelocity.Y = Mathf.MoveToward(_camVelocity.Y, desiredVelY, accelRateY * dt);

        if (Mathf.Abs(_camVelocity.X) < StopSpeedEpsilon) _camVelocity.X = 0f;
        if (Mathf.Abs(_camVelocity.Y) < StopSpeedEpsilon) _camVelocity.Y = 0f;

        // Apply velocity step (no overshoot)
        Vector2 nextPos = camPos + _camVelocity * dt;

        nextPos.X = ClampNoOvershoot(camPos.X, nextPos.X, finalTarget.X);
        nextPos.Y = ClampNoOvershoot(camPos.Y, nextPos.Y, finalTarget.Y);

        Vector2 clamped = ClampToWorld(nextPos);

        // If clamped, kill velocity into the wall
        if (!Mathf.IsEqualApprox(clamped.X, nextPos.X)) _camVelocity.X = 0f;
        if (!Mathf.IsEqualApprox(clamped.Y, nextPos.Y)) _camVelocity.Y = 0f;

        GlobalPosition = clamped;
    }

    private void ApplyRubberBandStop(float dt)
    {
        float decel = BaseFollowSpeed / Mathf.Max(0.001f, AccelDecelTimeSeconds);

        _camVelocity.X = Mathf.MoveToward(_camVelocity.X, 0f, decel * dt);
        _camVelocity.Y = Mathf.MoveToward(_camVelocity.Y, 0f, decel * dt);

        if (Mathf.Abs(_camVelocity.X) < StopSpeedEpsilon) _camVelocity.X = 0f;
        if (Mathf.Abs(_camVelocity.Y) < StopSpeedEpsilon) _camVelocity.Y = 0f;

        Vector2 camPos = GlobalPosition;
        Vector2 nextPos = camPos + _camVelocity * dt;

        Vector2 clamped = ClampToWorld(nextPos);

        if (!Mathf.IsEqualApprox(clamped.X, nextPos.X)) _camVelocity.X = 0f;
        if (!Mathf.IsEqualApprox(clamped.Y, nextPos.Y)) _camVelocity.Y = 0f;

        GlobalPosition = clamped;
    }

    private void GetWorldBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        if (!_boundsReady)
            RebuildWorldBounds();

        minX = _boundMinX;
        maxX = _boundMaxX;
        minY = _boundMinY;
        maxY = _boundMaxY;
    }

    private void GetWorldBoundsFromManual(out float minX, out float maxX, out float minY, out float maxY)
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
        GetWorldBounds(out float minX, out float maxX, out float minY, out float maxY);

        pos.X = Mathf.Clamp(pos.X, minX, maxX);
        pos.Y = Mathf.Clamp(pos.Y, minY, maxY);

        return pos;
    }

    private float ClampNoOvershoot(float current, float next, float target)
    {
        if (current < target) return Mathf.Min(next, target);
        if (current > target) return Mathf.Max(next, target);
        return target;
    }

    public override void _Draw()
    {
        if (!ShowDebug || _player == null)
            return;

        float worldW = DeadzoneWidthPx / Zoom.X;
        float worldH = DeadzoneHeightPx / Zoom.Y;

        Rect2 deadzoneRect = new Rect2(-worldW * 0.5f, -worldH * 0.5f, worldW, worldH);
        DrawRect(deadzoneRect, Colors.Cyan, false, 2f);

        Vector2 playerLocal = ToLocal(_player.GlobalPosition);
        DrawCircle(playerLocal, 6f, Colors.Red);
        DrawLine(Vector2.Zero, playerLocal, Colors.Red, 1f);

        DrawLine(Vector2.Zero, _camVelocity * 0.25f, Colors.Green, 3f);

        float barWidth = 220f;
        float barHeight = 8f;
        float visualRecentre = Mathf.Pow(_recentre01, 0.6f);

        Vector2 barPos = new Vector2(-barWidth * 0.5f, -(worldH * 0.5f) - 30f);
        DrawRect(new Rect2(barPos, new Vector2(barWidth, barHeight)), new Color(0.2f, 0.2f, 0.2f), true);
        DrawRect(new Rect2(barPos, new Vector2(barWidth * visualRecentre, barHeight)), Colors.Magenta, true);

        Color lockColor = _edgeLockDir == 0 ? new Color(0.4f, 0.4f, 0.4f) : Colors.Yellow;
        DrawCircle(new Vector2(0f, -(worldH * 0.5f) - 50f), 6f, lockColor);

        GetWorldBounds(out float minX, out float maxX, out float minY, out float maxY);
        float distLeft = GlobalPosition.X - minX;
        float distRight = maxX - GlobalPosition.X;
        float distTop = GlobalPosition.Y - minY;
        float distBottom = maxY - GlobalPosition.Y;

        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(-worldW * 0.5f, worldH * 0.5f + 20f),
            $"cam({GlobalPosition.X:0},{GlobalPosition.Y:0}) vel({_camVelocity.X:0.0},{_camVelocity.Y:0.0}) rec:{_recentre01:0.00} L:{distLeft:0} R:{distRight:0} T:{distTop:0} B:{distBottom:0} pY:{_player.GlobalPosition.Y:0} minY:{minY:0}",
            HorizontalAlignment.Left,
            -1,
            12,
            Colors.White
        );
    }
}
