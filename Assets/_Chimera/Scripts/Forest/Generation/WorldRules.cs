using System.Collections.Generic;

/// <summary>
/// Инварианты мира леса — аналог BodyRules для тел. Методы возвращают список нарушений;
/// пустой список = чисто. Проверяется машиной (тесты), а не словами. Лес, слайс s1.
/// </summary>
public static class WorldRules
{
    public static List<string> CheckConfig(WorldGenConfigSO cfg)
    {
        var issues = new List<string>();
        if (cfg == null)
        {
            issues.Add("конфиг мира — null");
            return issues;
        }
        if (cfg.octaves < 1 || cfg.octaves > 8)
            issues.Add($"octaves={cfg.octaves} вне [1, 8]");
        if (cfg.baseFrequency <= 0f)
            issues.Add($"baseFrequency={cfg.baseFrequency} должен быть > 0");
        if (cfg.amplitude < 0f)
            issues.Add($"amplitude={cfg.amplitude} должен быть >= 0");
        if (cfg.mapHalfExtent <= 0f)
            issues.Add($"mapHalfExtent={cfg.mapHalfExtent} должен быть > 0");
        if (cfg.maxSlopeDegrees <= 0f || cfg.maxSlopeDegrees > 60f)
            issues.Add($"maxSlopeDegrees={cfg.maxSlopeDegrees} вне (0, 60]");
        return issues;
    }

    /// <summary>
    /// Сетка resolution×resolution с шагом step: высоты конечны и в ±amplitude,
    /// макс. уклон в лимите конфига.
    /// </summary>
    public static List<string> CheckGrid(WorldGenConfigSO cfg, int resolution, float step)
    {
        var issues = new List<string>();
        issues.AddRange(CheckConfig(cfg));
        if (issues.Count > 0 || cfg == null) return issues;
        for (int iz = 0; iz < resolution; iz++)
            for (int ix = 0; ix < resolution; ix++)
            {
                float h = WorldHeightField.SampleHeight(cfg, ix * step, iz * step);
                if (float.IsNaN(h) || float.IsInfinity(h))
                {
                    issues.Add($"NaN/Inf в клетке ({ix},{iz})");
                    return issues;
                }
                if (h < -cfg.amplitude - 1e-4f || h > cfg.amplitude + 1e-4f)
                {
                    issues.Add($"высота {h} вне ±amplitude ({cfg.amplitude}) в клетке ({ix},{iz})");
                    return issues;
                }
            }
        float maxSlope = WorldHeightField.MaxSlopeDegrees(cfg, resolution, step);
        if (maxSlope > cfg.maxSlopeDegrees)
            issues.Add($"макс. уклон {maxSlope:F1}° > лимита {cfg.maxSlopeDegrees}°");
        return issues;
    }
}
