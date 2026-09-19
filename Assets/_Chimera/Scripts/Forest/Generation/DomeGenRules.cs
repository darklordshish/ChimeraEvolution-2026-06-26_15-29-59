using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Инварианты и формулы масштабирования павильона-купола (s10).
/// Один вход mapDiameter масштабирует всё: чанки, heightmap, капли эрозии,
/// тайлы NavMesh, кольцо, свод, cull-дистанции, цели флоры.
/// Методы возвращают список нарушений; пусто = чисто. Лес, слайс s10a.
/// </summary>
public static class DomeGenRules
{
    public static List<string> CheckConfig(DomeGenConfigSO cfg)
    {
        var issues = new List<string>();
        if (cfg == null)
        {
            issues.Add("конфиг купола — null");
            return issues;
        }
        if (cfg.mapDiameter < 100f || cfg.mapDiameter > 4000f)
            issues.Add($"mapDiameter={cfg.mapDiameter} вне [100, 4000]");
        if (cfg.chunkSize < 50f || cfg.chunkSize > 500f)
            issues.Add($"chunkSize={cfg.chunkSize} вне [50, 500]");
        if (cfg.mapDiameter / cfg.chunkSize > 16f)
            issues.Add($"сетка {cfg.mapDiameter / cfg.chunkSize:F1} чанков по стороне: больше 16 — дроби чанк, не карту");
        if (cfg.octaves < 1 || cfg.octaves > 8)
            issues.Add($"octaves={cfg.octaves} вне [1, 8]");
        if (cfg.ridgedOctaves < 1 || cfg.ridgedOctaves > 8)
            issues.Add($"ridgedOctaves={cfg.ridgedOctaves} вне [1, 8]");
        if (cfg.warpOctaves < 0 || cfg.warpOctaves > 8)
            issues.Add($"warpOctaves={cfg.warpOctaves} вне [0, 8]");
        if (cfg.baseFrequency <= 0f)
            issues.Add($"baseFrequency={cfg.baseFrequency} должен быть > 0");
        if (cfg.amplitude < 0f)
            issues.Add($"amplitude={cfg.amplitude} должен быть >= 0");
        if (cfg.maxSlopeDegrees <= 0f || cfg.maxSlopeDegrees > 60f)
            issues.Add($"maxSlopeDegrees={cfg.maxSlopeDegrees} вне (0, 60]");
        if (cfg.treeDensityPerM2 < 0f)
            issues.Add($"treeDensityPerM2={cfg.treeDensityPerM2} должен быть >= 0");
        if (cfg.forestFraction <= 0f || cfg.forestFraction > 1f)
            issues.Add($"forestFraction={cfg.forestFraction} вне (0, 1]");
        if (cfg.navVoxelSize < 0.05f || cfg.navVoxelSize > 1f)
            issues.Add($"navVoxelSize={cfg.navVoxelSize} вне [0.05, 1]");
        if (cfg.navTileSize < 16f)
            issues.Add($"navTileSize={cfg.navTileSize} должен быть >= 16");
        if (cfg.ringPeakHeight < 0f)
            issues.Add($"ringPeakHeight={cfg.ringPeakHeight} должен быть >= 0");
        if (cfg.vaultRadiusFactor <= 1f)
            issues.Add($"vaultRadiusFactor={cfg.vaultRadiusFactor} должен быть > 1");
        return issues;
    }

    /// <summary>Число чанков по стороне: ceil(D / chunk). D=2000, chunk=250 → 8.</summary>
    public static int ChunkCount(DomeGenConfigSO cfg)
    {
        return Mathf.CeilToInt(cfg.mapDiameter / cfg.chunkSize);
    }

    /// <summary>
    /// Рекомендуемый heightmap: 2^ceil(log2(D / 0.6)), кламп [2048, 4096].
    /// D=2000 → 4096 (тексель 0.49м, речка 3м влезает); D≤1300 → 2048.
    /// </summary>
    public static int RecommendHeightmapSize(float mapDiameter)
    {
        int n = Mathf.CeilToInt(Mathf.Log(mapDiameter / 0.6f, 2f));
        return Mathf.Clamp(1 << n, 2048, 4096);
    }

    /// <summary>Капли эрозии: плотность 0.177 кап/м² × площадь круга. R1000 → ~556k.</summary>
    public static int RecommendDropCount(float mapDiameter)
    {
        float r = mapDiameter * 0.5f;
        return Mathf.RoundToInt(0.177f * Mathf.PI * r * r);
    }

    /// <summary>Радиус свода: 1.4 × R. D=2000 → 1400.</summary>
    public static float VaultRadius(DomeGenConfigSO cfg)
    {
        return cfg.vaultRadiusFactor * cfg.mapDiameter * 0.5f;
    }

    /// <summary>Дальняя плоскость камеры: 4 × R. D=2000 → 4000.</summary>
    public static float FarPlane(float mapDiameter)
    {
        return 4f * mapDiameter * 0.5f;
    }

    /// <summary>
    /// Цель деревьев: плотность × доля леса × площадь − 20% на вычеты
    /// (поляны/POI/берега/лаборатория). Дефолт на Ø2000 → ~41k (коридор спеки 35–45k).
    /// </summary>
    public static int TreeTarget(DomeGenConfigSO cfg)
    {
        float r = cfg.mapDiameter * 0.5f;
        return Mathf.RoundToInt(0.8f * cfg.treeDensityPerM2 * cfg.forestFraction * Mathf.PI * r * r);
    }

    /// <summary>
    /// Разрешение сида (чистая функция ради тестов): randomSeed → внешний тик
    /// (в игре Environment.TickCount, как у ArenaWalls), иначе поле seed.
    /// </summary>
    public static int ResolveSeed(DomeGenConfigSO cfg, int tick)
    {
        if (cfg == null) return tick;
        return cfg.randomSeed ? tick : unchecked((int)cfg.seed);
    }
}
