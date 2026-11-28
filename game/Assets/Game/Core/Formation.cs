namespace Futebol.Core
{
    /// <summary>
    /// Posições-base do 4-4-2, para um time que ataca o lado +X.
    /// X vai de -1 (próprio gol) a +1 (gol adversário); Z de -1 a +1 (largura).
    /// </summary>
    public static class Formation442
    {
        public static readonly Vec2[] Slots =
        {
            new Vec2(-0.95f, 0f),     // GK
            new Vec2(-0.62f, -0.70f), // RB
            new Vec2(-0.70f, -0.22f), // CB
            new Vec2(-0.70f, 0.22f),  // CB
            new Vec2(-0.62f, 0.70f),  // LB
            new Vec2(-0.25f, -0.72f), // RW
            new Vec2(-0.32f, -0.20f), // CM
            new Vec2(-0.32f, 0.20f),  // CM
            new Vec2(-0.25f, 0.72f),  // LW
            new Vec2(-0.05f, -0.14f), // ST
            new Vec2(-0.05f, 0.14f),  // ST
        };
    }
}
