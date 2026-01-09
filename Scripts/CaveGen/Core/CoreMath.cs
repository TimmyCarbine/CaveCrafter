using System;

namespace CaveCrafter.CaveGen.Core
{
    /// <summary>
    /// Small math utilities used by the cave generation core.
    /// Keeping these here avoids sprinkling ad-hoc math all over the project.
    /// </summary>
    public static class CoreMath
    {
        // -------------------------
        // Clamping
        // -------------------------

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        // -------------------------
        // Safe conversions
        // -------------------------

        public static int FloorToInt(float value) => (int)MathF.Floor(value);
        public static int CeilToInt(float value) => (int)MathF.Ceiling(value);
        public static int RoundToInt(float value) => (int)MathF.Round(value);

        // -------------------------
        // Lerp / remap
        // -------------------------

        public static float Lerp(float a, float b, float t)
        {
            t = Clamp01(t);
            return a + (b - a) * t;
        }

        public static float InverseLerp(float a, float b, float value)
        {
            if (MathF.Abs(b - a) <= 1e-6f) return 0f;
            return Clamp01((value - a) / (b - a));
        }

        public static float Remap(float inMin, float inMax, float outMin, float outMax, float value)
        {
            float t = InverseLerp(inMin, inMax, value);
            return Lerp(outMin, outMax, t);
        }

        // -------------------------
        // Curves (used for edge bias shaping)
        // -------------------------

        /// <summary>
        /// Smoothstep curve: 3t^2 - 2t^3. Good for easing.
        /// </summary>
        public static float SmoothStep01(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Ease towards edges: maps t in [0..1] to a value that pushes towards 0 and 1.
        /// amount in [0..1] controls strength (0 = no change, 1 = strong push).
        /// This is used to bias outer highways towards map edges.
        /// </summary>
        public static float EaseToEdges01(float t, float amount)
        {
            t = Clamp01(t);
            amount = Clamp01(amount);

            // Base "push to edges" function: (2t - 1)^3 mapped back to [0..1]
            float centered = (2f * t) - 1f;           // [-1..1]
            float pushed = centered * centered * centered; // [-1..1] with edge emphasis
            float edgeBiased = (pushed + 1f) * 0.5f;  // [0..1]

            // Blend between original and edge-biased.
            return Lerp(t, edgeBiased, amount);
        }
    }
}
