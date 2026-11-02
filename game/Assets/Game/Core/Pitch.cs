namespace Futebol.Core
{
    /// <summary>Medidas oficiais do campo, em metros. Centro do campo na origem.</summary>
    public static class Pitch
    {
        public const float Length = 105f;
        public const float Width = 68f;
        public const float HalfLength = Length / 2f;   // 52.5
        public const float HalfWidth = Width / 2f;     // 34
        public const float GoalHalfWidth = 7.32f / 2f; // 3.66
        public const float GoalHeight = 2.44f;
        public const float BoxDepth = 16.5f;
        public const float BoxHalfWidth = 20.16f;
        public const float SixYardDepth = 5.5f;
        public const float CenterCircleRadius = 9.15f;
        public const float PenaltySpot = 11f;
        public const float BallRadius = 0.11f;

        /// <summary>Gol que o time ataca. dir = +1 ataca o lado +X.</summary>
        public static Vec2 GoalCenter(int dir) => new Vec2(HalfLength * dir, 0f);

        public static bool InsideField(Vec2 p) =>
            p.X >= -HalfLength && p.X <= HalfLength && p.Z >= -HalfWidth && p.Z <= HalfWidth;

        public static bool InsideBox(Vec2 p, int goalSide) =>
            p.X * goalSide >= HalfLength - BoxDepth && p.X * goalSide <= HalfLength && System.MathF.Abs(p.Z) <= BoxHalfWidth;
    }
}
