using System;

namespace Futebol.Core
{
    /// <summary>Ponto ou direção no plano do campo. X = comprimento (gol a gol), Z = largura.</summary>
    public struct Vec2
    {
        public float X, Z;
        public Vec2(float x, float z) { X = x; Z = z; }
        public static readonly Vec2 Zero = new Vec2(0, 0);
        public float Length => MathF.Sqrt(X * X + Z * Z);
        public float SqrLength => X * X + Z * Z;
        public Vec2 Normalized { get { var l = Length; return l > 1e-5f ? new Vec2(X / l, Z / l) : Zero; } }
        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Z * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Z * s);
        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Z * b.Z;
        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public Vec2 Rotated(float radians)
        {
            float c = MathF.Cos(radians), s = MathF.Sin(radians);
            return new Vec2(X * c - Z * s, X * s + Z * c);
        }
        public Vec2 ClampLength(float max) { var l = Length; return l > max ? this * (max / l) : this; }
        public bool IsFinite => float.IsFinite(X) && float.IsFinite(Z);
        public override string ToString() => $"({X:0.0}, {Z:0.0})";
    }

    /// <summary>Posição da bola com altura (Y).</summary>
    public struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public Vec2 Flat => new Vec2(X, Z);
        public static Vec3 FromFlat(Vec2 v, float y) => new Vec3(v.X, y, v.Z);
        public float FlatLength => MathF.Sqrt(X * X + Z * Z);
        public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
    }

    /// <summary>Gerador aleatório com semente, para a partida ser reproduzível.</summary>
    public sealed class Rng
    {
        private readonly Random _r;
        public Rng(int seed) { _r = new Random(seed); }
        public float Value => (float)_r.NextDouble();
        public float Range(float min, float max) => min + (max - min) * Value;
        public int Range(int minInclusive, int maxExclusive) => _r.Next(minInclusive, maxExclusive);
        public bool Chance(float p) => Value < p;
    }
}
