using UnityEngine;

[CreateAssetMenu(menuName = "Chimera/Forest/DomeGenConfig", fileName = "DomeGenConfig")]
public class DomeGenConfigSO : ScriptableObject
{
    [Header("Сид: виден в Inspector, пишется в лог с инструкцией (как ArenaWalls)")]
    public long seed = 1337;
    public bool randomSeed = true;

    [Header("Версия (смена алгоритма = смена version)")]
    public int version = 1;

    // S1 (2026-11-01): у ПОЛЯ своя версия. 0/1 — v1 бит-в-бит (A/C/B0),
    // 2 — три полосы H0. `version` остаётся солью v1 и не переиспользуется:
    // иначе смена полос сдвинула бы A/C. 0 приходит у старых ассетов/сцен
    // (гоча нового сериализованного поля) и читается как v1.
    [Header("Версия поля: 0 = не настроено = v1, 2 = полосы H0 (S1)")]
    public int worldVersion = 0;

    [Header("Полосы H0 v2: метры на Ø2000, масштабируются D/2000")]
    public float macroLambda = 1600f;
    [Tooltip("Полный перепад P, м (амплитуда A = P/2)")]
    public float macroAmplitude = 56f;
    [Range(0, 4)] public int macroOctaves = 2;
    [Range(0f, 0.12f)] public float macroWarp = 0.10f;

    public float mesoLambda = 450f;
    public float mesoAmplitude = 36f;
    [Range(0, 4)] public int mesoOctaves = 3;
    [Range(0f, 0.12f)] public float mesoWarp = 0.10f;
    [Tooltip("Ridged только на мезо и только под маской (S1 §3.2)")]
    public bool mesoRidged = true;

    public float microLambda = 90f;
    public float microAmplitude = 6f;
    [Range(0, 4)] public int microOctaves = 2;
    [Range(0f, 0.12f)] public float microWarp = 0.08f;

    [Header("Классы уклона S1 §11: ходьба ≤ walk, стена > maxSlopeDegrees")]
    public float walkMaxDegrees = DefaultWalkMaxDegrees;

    /// <summary>Граница «проходимо» по S1 §11.3; 0 в поле = «не настроено».</summary>
    public const float DefaultWalkMaxDegrees = 22f;

    /// <summary>Первая (и по умолчанию действующая) версия поля.</summary>
    public const int FirstWorldVersion = 1;

    // ЕДИНСТВЕННАЯ точка правила «0 → v1». Зовут оба входа: <see cref="WorldVersion"/>
    // на конфиге и <see cref="ForestBuilder.WorldVersion"/> в сцене; своего `> 0 ? : 1`
    // больше нигде нет, иначе два поля дадут две трактовки одного нуля.
    /// <summary>Разрешить версию поля: 0/отрицательное = «не настроено» = v1.</summary>
    public static int ResolveWorldVersion(int rawVersion)
    {
        return rawVersion > 0 ? rawVersion : FirstWorldVersion;
    }

    /// <summary>0 в поле = «не настроено» = v1: новое поле у старой сцены приходит нулём.</summary>
    public int WorldVersion => ResolveWorldVersion(worldVersion);

    /// <summary>0 в поле = «не настроено» → 22 (гоча нового сериализованного поля).</summary>
    public float WalkMaxDegrees => walkMaxDegrees > 0f ? walkMaxDegrees : DefaultWalkMaxDegrees;

    /// <summary>Граница стены = существующий предел флоры; второго литерала 35 нет.</summary>
    public float WallMinDegrees => maxSlopeDegrees > 0f ? maxSlopeDegrees : DefaultWallMinDegrees;

    [Header("Размер: один параметр масштабирует всё (формулы — DomeGenRules)")]
    public float mapDiameter = 2000f;
    public float chunkSize = 250f;

    [Header("Рельеф: FBM + ridged с маской гор + domain warp")]
    public int octaves = 5;
    public int ridgedOctaves = 5;
    public int warpOctaves = 4;
    public float warpStrength = 6f;
    public float baseFrequency = 0.02f;
    public float amplitude = 6f;
    public float maxSlopeDegrees = DefaultWallMinDegrees;
    /// <summary>Граница «стена» по S1 §11.3 — она же предел флоры (35).</summary>
    public const float DefaultWallMinDegrees = 35f;

    [Header("Вода (s10b): пока ЗАГЛУШКА числом, как в s8")]
    public float waterLevel = -1.5f;

    // S3 (2026-11-01): P5-lite — СВОЙ слой формы, а не часть B-lite. Пока сцена
    // `ForestPavilion` не получила флаг явно (это S1 шаг 8), v1 обязан остаться
    // бит-в-бит: там действует историческая связка `patchyFlora → рельеф`.
    [Header("Рельеф формы (S3): отдельный слой; 0 у старой сцены = историческая связка с patchyFlora")]
    public bool buildRelief = false;

    /// <summary>
    /// Включён ли слой рельефа. Для v1 — история (patchyFlora), чтобы отпечаток
    /// Pavilion не поехал до S1 шага 8; для v2 — только явный <see cref="buildRelief"/>.
    /// Двух источников нет: решение в одном свойстве.
    /// </summary>
    public bool ReliefEnabled => buildRelief || (WorldVersion <= 1 && patchyFlora);

    [Header("Плотности (s10d): цели выводятся формулой, не числом")]
    public float treeDensityPerM2 = 0.03f;
    /// <summary>0 = старые 700; иначе целевое число деревьев на стенд.</summary>
    public int treeCount = 0;
    /// <summary>Пятна леса/поляны по низкочастотному fBm; выключено для A/C.</summary>
    public bool patchyFlora = false;
    public float forestFraction = 0.55f;

    [Header("NavMesh: ТОЛЬКО тайлами (моно-бейк на Ø2000 запрещён)")]
    public float navVoxelSize = 0.35f;
    public float navTileSize = 112f;

    [Header("Купол: пик константа, радиусы — от диаметра")]
    public float ringPeakHeight = 100f;
    public float vaultRadiusFactor = 1.4f;

    [Header("Fast-preview (20–45с вместо 70–140с полного бейка)")]
    public bool fastPreview = false;
    public int previewHeightmapSize = 2048;

    void OnValidate()
    {
        mapDiameter = Mathf.Clamp(mapDiameter, 100f, 4000f);
        chunkSize = Mathf.Clamp(chunkSize, 50f, 500f);
        octaves = Mathf.Clamp(octaves, 1, 8);
        ridgedOctaves = Mathf.Clamp(ridgedOctaves, 1, 8);
        warpOctaves = Mathf.Clamp(warpOctaves, 0, 8);
        warpStrength = Mathf.Max(warpStrength, 0f);
        baseFrequency = Mathf.Max(baseFrequency, 0.0001f);
        amplitude = Mathf.Max(amplitude, 0f);
        maxSlopeDegrees = Mathf.Clamp(maxSlopeDegrees, 1f, 60f);
        if (walkMaxDegrees > 0f) walkMaxDegrees = Mathf.Clamp(walkMaxDegrees, 1f, 60f);
        if (worldVersion < 0) worldVersion = 0;
        macroLambda = Mathf.Max(macroLambda, 1f);
        mesoLambda = Mathf.Max(mesoLambda, 1f);
        microLambda = Mathf.Max(microLambda, 1f);
        macroAmplitude = Mathf.Max(macroAmplitude, 0f);
        mesoAmplitude = Mathf.Max(mesoAmplitude, 0f);
        microAmplitude = Mathf.Max(microAmplitude, 0f);
        macroOctaves = Mathf.Clamp(macroOctaves, 0, 4);
        mesoOctaves = Mathf.Clamp(mesoOctaves, 0, 4);
        microOctaves = Mathf.Clamp(microOctaves, 0, 4);
        macroWarp = Mathf.Clamp(macroWarp, 0f, 0.12f);
        mesoWarp = Mathf.Clamp(mesoWarp, 0f, 0.12f);
        microWarp = Mathf.Clamp(microWarp, 0f, 0.12f);
        treeDensityPerM2 = Mathf.Max(treeDensityPerM2, 0f);
        forestFraction = Mathf.Clamp01(forestFraction);
        navVoxelSize = Mathf.Clamp(navVoxelSize, 0.05f, 1f);
        navTileSize = Mathf.Max(navTileSize, 16f);
        ringPeakHeight = Mathf.Max(ringPeakHeight, 0f);
        vaultRadiusFactor = Mathf.Max(vaultRadiusFactor, 1.01f);
    }
}
