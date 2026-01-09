using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace CaveCrafter.CaveGen.Core.Geometry
{
    /// <summary>
    /// Lightweight 2D integer vector for tile coordinates.
    /// Used mainly in rasterisation.
    /// </summary>
    public readonly struct Int2 : IEquatable<Int2>
    {
        public readonly int X, Y;

        public Int2(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Int2 Zero => new Int2(0, 0);

        public static Int2 operator +(Int2 a, Int2 b) => new Int2(a.X + b.X, a.Y + b.Y);
        public static Int2 operator -(Int2 a, Int2 b) => new Int2(a.X - b.X, a.Y - b.Y);

        public override string ToString() => $"({X}, {Y})";

        public bool Equals(Int2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Int2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(Int2 a, Int2 b) => a.Equals(b);
        public static bool operator !=(Int2 a, Int2 b) => !a.Equals(b);
    }
}