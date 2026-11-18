namespace Chimera.Laws
{
    /// <summary>Float-примитивы дословно как `Mathf` (сравнения, а не `MathF.Max`): расходятся они на нуле со знаком и NaN,
    /// а закон обязан совпадать с прежним кодом игры бит в бит.</summary>
    internal static class LawMath
    {
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
