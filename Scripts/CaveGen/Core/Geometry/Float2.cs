using System;
using System.Diagnostics.CodeAnalysis;

namespace CaveCrafter.CaveGen.Core.Geometry
{
    /// <summary>
    /// Lightweight 2D float vector used by the cave generation core.
    /// This keeps generation code independent of Godot's Vector2 so it can be unit tested easily.
    /// </summary>
    public readonly struct Float2 : IEquatable<Float2>
    {
        public readonly float X;
        public readonly float Y;

        public Float2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Float2 Zero => new Float2(0f, 0f);
        public static Float2 One => new Float2(1f, 1f);

        // === BASIC VECTOR OPERATIONS ===
        public float Length => MathF.Sqrt((X * X) + (Y * Y));
        public float LengthSquared => (X * X) + (Y * Y);

        public Float2 Normalized
        {
            get
            {
                float len = Length;
                if (len <= 1e-6f) return Zero;
                return new Float2(X / len, Y / len);
            }
        }

        public static Float2 operator +(Float2 a, Float2 b) => new Float2(a.X + b.X, a.Y + b.Y);
        public static Float2 operator -(Float2 a, Float2 b) => new Float2(a.X - b.X, a.Y - b.Y);
        public static Float2 operator *(Float2 v, float s) => new Float2(v.X * s, v.Y * s);
        public static Float2 operator *(float s, Float2 v) => new Float2(v.X * s, v.Y * s);
        public static Float2 operator /(Float2 v, float s) => new Float2(v.X / s, v.Y / s);

        public static float Dot(Float2 a, Float2 b) => (a.X * b.X) + (a.Y * b.Y);

        public static float Distance(Float2 a, Float2 b) => (a - b).Length;
        public static float DistanceSquared(Float2 a, Float2 b) => (a - b).LengthSquared;

        public static Float2 Lerp(Float2 a, Float2 b, float t)
        {
            t = CoreMath.Clamp01(t);
            return new Float2(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t
            );
        }

        public override string ToString() => $"({X:0.###}, {Y:0.###})";

        // === EQUALITY ===
        public bool Equals(Float2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Float2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(Float2 a, Float2 b) => a.Equals(b);
        public static bool operator !=(Float2 a, Float2 b) => !a.Equals(b);
    }
}