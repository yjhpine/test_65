namespace ActionPlatformer.Units.Bosses
{
    internal static class BossPatternChecks
    {
        public static bool Positive(float v) => v > 0 && !float.IsInfinity(v);
        public static bool NonNegative(float v) => v >= 0 && !float.IsInfinity(v);
    }
}
