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
        if (cfg.warpStrength < 0f)
            issues.Add($"warpStrength={cfg.warpStrength} должен быть >= 0");
        if (cfg.baseFrequency <= 0f)
            issues.Add($"baseFrequency={cfg.baseFrequency} должен быть > 0");
        if (cfg.amplitude < 0f)
            issues.Add($"amplitude={cfg.amplitude} должен быть >= 0");
        if (cfg.maxSlopeDegrees <= 0f || cfg.maxSlopeDegrees > 60f)
            issues.Add($"maxSlopeDegrees={cfg.maxSlopeDegrees} вне (0, 60]");
        // S1 §11.3: две границы классов из конфига, не литералы; 0 в walkMax — «не настроено».
        if (cfg.WalkMaxDegrees >= cfg.WallMinDegrees)
            issues.Add($"walkMaxDegrees={cfg.WalkMaxDegrees} должно быть < wallMinDegrees={cfg.WallMinDegrees}");
        if (cfg.worldVersion < 0 || cfg.worldVersion > 2)
            issues.Add($"worldVersion={cfg.worldVersion} вне [0, 2] (0/1 = v1, 2 = полосы)");
        if (cfg.WorldVersion >= 2)
            CheckBands(cfg, issues);
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

    /// <summary>
    /// S1 §3.2/§11.4: полосы v2 и потолки наклона. Числа здесь — не «ещё одни настройки»,
    /// а инвариант: <c>Σ πP/λ ≤ 0.700 = tan 35°</c> и каждая полоса в своём потолке.
    /// Проверка идёт по метрам на Ø2000: масштаб D/2000 у λ и P общий и сокращается.
    /// </summary>
    static void CheckBands(DomeGenConfigSO cfg, List<string> issues)
    {
        if (cfg.macroLambda <= 0f) issues.Add($"macroLambda={cfg.macroLambda} должен быть > 0");
        if (cfg.mesoLambda <= 0f) issues.Add($"mesoLambda={cfg.mesoLambda} должен быть > 0");
        if (cfg.microLambda <= 0f) issues.Add($"microLambda={cfg.microLambda} должен быть > 0");
        if (cfg.macroAmplitude < 0f) issues.Add($"macroAmplitude={cfg.macroAmplitude} должен быть >= 0");
        if (cfg.mesoAmplitude < 0f) issues.Add($"mesoAmplitude={cfg.mesoAmplitude} должен быть >= 0");
        if (cfg.microAmplitude < 0f) issues.Add($"microAmplitude={cfg.microAmplitude} должен быть >= 0");
        if (cfg.macroOctaves < 0 || cfg.macroOctaves > 4)
            issues.Add($"macroOctaves={cfg.macroOctaves} вне [0, 4]");
        if (cfg.mesoOctaves < 0 || cfg.mesoOctaves > 4)
            issues.Add($"mesoOctaves={cfg.mesoOctaves} вне [0, 4]");
        if (cfg.microOctaves < 0 || cfg.microOctaves > 4)
            issues.Add($"microOctaves={cfg.microOctaves} вне [0, 4]");
        CheckWarp("macro", cfg.macroWarp, issues);
        CheckWarp("meso", cfg.mesoWarp, issues);
        CheckWarp("micro", cfg.microWarp, issues);

        float macro = BandBudget(cfg.macroAmplitude, cfg.macroLambda);
        float meso = BandBudget(cfg.mesoAmplitude, cfg.mesoLambda);
        float micro = BandBudget(cfg.microAmplitude, cfg.microLambda);
        if (macro > 0.19f + 1e-4f)
            issues.Add($"макро-полоса: πP/λ = {macro:F3} > 0.19 (S1 §3.2)");
        if (meso > 0.30f + 1e-4f)
            issues.Add($"мезо-полоса: πP/λ = {meso:F3} > 0.30 (S1 §11.4)");
        if (micro > 0.21f + 1e-4f)
            issues.Add($"микро-полоса: πP/λ = {micro:F3} > 0.21 (S1 §11.4)");
        float sum = macro + meso + micro;
        if (sum > 0.700f + 1e-4f)
            issues.Add($"сумма полос πP/λ = {sum:F3} > 0.700 = tan 35°");
        if (cfg.macroAmplitude + 1e-4f < 1.3f * cfg.mesoAmplitude)
            issues.Add($"P_macro={cfg.macroAmplitude} < 1.3·P_meso={cfg.mesoAmplitude} (S1 §3.2: связь амплитуд)");
        if (cfg.mesoAmplitude > 0.0955f * cfg.mesoLambda + 1e-3f)
            issues.Add($"P_meso={cfg.mesoAmplitude} > 0.0955·λ={0.0955f * cfg.mesoLambda:F1} (пара P/λ)");
        if (cfg.microAmplitude > 0.0668f * cfg.microLambda + 1e-3f)
            issues.Add($"P_micro={cfg.microAmplitude} > 0.0668·λ={0.0668f * cfg.microLambda:F1} (пара P/λ)");
    }

    static float BandBudget(float amplitude, float lambda)
    {
        return lambda > 0f ? Mathf.PI * amplitude / lambda : 0f;
    }

    static void CheckWarp(string band, float warp, List<string> issues)
    {
        if (warp < 0f || warp > 0.12f)
            issues.Add($"{band}Warp={warp} вне [0, 0.12] (S1 §3.2: A_w ≤ 0.12·λ)");
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
