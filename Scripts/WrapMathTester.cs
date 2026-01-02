using Godot;

public partial class WrapMathTester : Node
{
    [Export] public float WorldWidthPx = 8192f;
    [Export] public float CameraX = 100f;
    [Export] public float ObjectX = 8100f;

    public override void _Ready()
    {
        float wrapped = WrapMath.WrapRange(ObjectX, 0f, WorldWidthPx);
        float delta = WrapMath.ShortestDelta(CameraX, ObjectX, WorldWidthPx);
        float nearest = WrapMath.NearestImageX(ObjectX, CameraX, WorldWidthPx);

        GD.Print($"WorldWidthPx = {WorldWidthPx}");
        GD.Print($"ObjectX = {ObjectX} WrapRange -> {wrapped}");
        GD.Print($"ShortestDelta(cam = {CameraX} to obj = {ObjectX}) -> {delta}");
        GD.Print($"NearestImageX(obj = {ObjectX}, cam = {CameraX}) -> {nearest}");
    }
}