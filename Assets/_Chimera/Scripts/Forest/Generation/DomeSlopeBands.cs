using UnityEngine;

/// <summary>
/// S2 §4: полосы склона для материала чанка. Пороги — градусы; к границе добавляется
/// детерминированный шум ±2°, чтобы полоса не читалась горизонталью. Сам выбор материала
/// по сабмешам — S3 шаг 6; здесь одна функция, чтобы чанк, отчёт и кадр мерили одинаково.
/// </summary>
public static class DomeSlopeBands
{
    public const int Grass = 0;
    public const int Turf = 1;
    public const int Scree = 2;
    public const int Rock = 3;
    public const int Cliff = 4;

    /// <summary>Порог полосы в градусах (до шума).</summary>
    public static readonly float[] Thresholds = { 10f, 18f, 26f, 35f };

    /// <summary>Индекс полосы 0..4 по уклону с шумом ±2° от (seed, x, z).</summary>
    public static int Index(float slopeDegrees, float x, float z, long seed)
    {
        float n = Noise(x, z, seed) * 2f; // −2…+2
        float s = slopeDegrees + n;
        if (s < Thresholds[0]) return Grass;
        if (s < Thresholds[1]) return Turf;
        if (s < Thresholds[2]) return Scree;
        if (s < Thresholds[3]) return Rock;
        return Cliff;
    }

    public static Color ColorOf(int band)
    {
        switch (band)
        {
            case Turf: return DomePalette.Turf;
            case Scree: return DomePalette.Scree;
            case Rock: return DomePalette.Rock;
            case Cliff: return DomePalette.Rock * 0.85f;
            default: return DomePalette.Terrain;
        }
    }

    /// <summary>Высотный тинт — только значение (S2 §4): ниже +2 ×0,92, выше +24 ×1,10.</summary>
    public static Color HeightTint(float groundY, float lakeLevel)
    {
        float h = groundY - lakeLevel;
        if (h < 2f) return new Color(0.92f, 0.92f, 0.94f);
        if (h > 24f) return new Color(1.10f, 1.10f, 1.10f);
        if (h > 12f) return new Color(1.05f, 1.05f, 1.03f);
        return Color.white;
    }

    static float Noise(float x, float z, long seed)
    {
        // Детерминированный, без UnityEngine.Random: та же точка — тот же сдвиг.
        float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 9911,
            Mathf.RoundToInt(x * 0.25f) * 7919 + Mathf.RoundToInt(z * 0.25f)));
        float b = SeededHash.ToFloat01(SeededHash.Hash(seed, 9912,
            Mathf.RoundToInt(z * 0.25f) * 7919 + Mathf.RoundToInt(x * 0.25f)));
        return Mathf.Clamp((a + b) - 1f, -1f, 1f);
    }
}
