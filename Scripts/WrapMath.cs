using Godot;

public static class WrapMath
{
    /// <summary>
    /// Wraps x into the range [min, max)
    /// Example: min=0, max=100 -> result is 0..99.999...
    /// </summary>
    public static float WrapRange(float x, float min, float max)
    {
        float width = max - min;
        if (width <= 0f) return min;

        float t = (x - min) % width;
        if (t < 0f) t += width;

        return min + t;
    }

    /// <summary>
    /// Returns the shortest signed delta from 'from' to 'to' on a wrapping ring of size width.
    /// Result is in [-width/2 .. +width/2).
    /// </summary>
    public static float ShortestDelta(float from, float to, float width)
    {
        if (width <= 0f) return to - from;

        float d = (to - from) % width;
        if (d > width * 0.5f) d -= width;
        if (d < -width * 0.5f) d += width;

        return d;
    }

    /// <summary>
    /// Given an object's X and a camera X, returns an object's render X
    /// positioned at the nearest wrapped copy relative to the camera
    /// </summary>
    public static float NearestImageX(float objectX, float cameraX, float width)
    {
        float dx = ShortestDelta(cameraX, objectX, width);
        return cameraX + dx;
    }
}