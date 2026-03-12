using System;

namespace SpaceSim.Shared.Math
{
    /// <summary>
    /// Lightweight 3D vector for simulation layer. No UnityEngine dependency.
    /// Uses double precision for orbital calculations.
    /// </summary>
    public readonly struct SimVec3 : IEquatable<SimVec3>
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public SimVec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly SimVec3 Zero = new SimVec3(0, 0, 0);
        public static readonly SimVec3 One = new SimVec3(1, 1, 1);
        public static readonly SimVec3 Up = new SimVec3(0, 1, 0);

        public double SqrMagnitude => X * X + Y * Y + Z * Z;
        public double Magnitude => System.Math.Sqrt(SqrMagnitude);

        public SimVec3 Normalized
        {
            get
            {
                double m = Magnitude;
                if (m < 1e-15) return Zero;
                return new SimVec3(X / m, Y / m, Z / m);
            }
        }

        // Arithmetic operators.
        public static SimVec3 operator +(SimVec3 a, SimVec3 b) =>
            new SimVec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static SimVec3 operator -(SimVec3 a, SimVec3 b) =>
            new SimVec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static SimVec3 operator *(SimVec3 v, double s) =>
            new SimVec3(v.X * s, v.Y * s, v.Z * s);

        public static SimVec3 operator *(double s, SimVec3 v) => v * s;

        public static SimVec3 operator -(SimVec3 v) =>
            new SimVec3(-v.X, -v.Y, -v.Z);

        public static double Dot(SimVec3 a, SimVec3 b) =>
            a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static double Distance(SimVec3 a, SimVec3 b) =>
            (a - b).Magnitude;

        // Equality.
        public bool Equals(SimVec3 other) =>
            X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) =>
            obj is SimVec3 other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(X, Y, Z);

        public static bool operator ==(SimVec3 a, SimVec3 b) => a.Equals(b);
        public static bool operator !=(SimVec3 a, SimVec3 b) => !a.Equals(b);

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }
}
