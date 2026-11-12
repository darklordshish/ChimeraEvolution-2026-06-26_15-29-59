using UnityEngine;

/// <summary>
/// Разрешения, на которых один и тот же пол читают разные потребители.
/// Гарантия обязана называть разрешение: «связно» без клетки — не гарантия.
/// <para>
/// S1 (2026-11-01): контракт поля получает <see cref="lambdaMin"/> (самая короткая
/// волна октавы) и <see cref="classEpsilon"/> — единственную линейку класса уклона.
/// <see cref="slopeEpsilon"/> остаётся шагом выборки уклона (сетка маршрута), а НЕ
/// второй линейкой класса: класс меряется только <see cref="classEpsilon"/> = 1 м.
/// </para>
/// </summary>
public readonly struct SamplingContract
{
    /// <summary>Диаметр, на котором объявлены полосы `H0` v2 (S1 §12, P0-6).</summary>
    public const float BandProfileDiameter = 2000f;

    /// <summary>
    /// Минимум квадов на чанк, при котором самая короткая волна микро-полосы
    /// (`λ_min = 45 м` на Ø2000) держится выше `4·meshStep = 20.8 м`. На 16–24 квадах
    /// `4·meshStep` больше λ_min и октава алиасит — профиль не объявлен.
    /// </summary>
    public const int BandProfileMinQuads = 48;

    public readonly float meshStep;
    public readonly float slopeEpsilon;
    public readonly float routeCell;
    public readonly float navVoxel;
    /// <summary>Самая короткая волна октавы поля, м (S1 §3.2: ≥ 4·<see cref="meshStep"/>).</summary>
    public readonly float lambdaMin;
    /// <summary>EPS линейки класса уклона, м: одна на проект (S1 §11.3).</summary>
    public readonly float classEpsilon;
    /// <summary>Граница «проходимо» (22) из конфига — часть контракта мира.</summary>
    public readonly float walkMaxDegrees;
    /// <summary>Граница «стена» (35) из конфига — часть контракта мира.</summary>
    public readonly float wallMinDegrees;

    public SamplingContract(float meshStep, float slopeEpsilon, float routeCell, float navVoxel,
        float lambdaMin, float classEpsilon, float walkMaxDegrees, float wallMinDegrees)
    {
        this.meshStep = meshStep;
        this.slopeEpsilon = slopeEpsilon;
        this.routeCell = routeCell;
        this.navVoxel = navVoxel;
        this.lambdaMin = lambdaMin;
        this.classEpsilon = classEpsilon;
        this.walkMaxDegrees = walkMaxDegrees;
        this.wallMinDegrees = wallMinDegrees;
    }

    public static SamplingContract From(DomeGenConfigSO cfg, int terrainQuads, int routeRes = 128)
    {
        if (cfg == null) return default;
        float mesh = cfg.chunkSize / Mathf.Max(4, terrainQuads);
        float slope = Mathf.Max(0.5f, mesh * 0.5f);
        float route = cfg.mapDiameter / Mathf.Max(32, routeRes);
        float nav = cfg.navVoxelSize > 0f ? cfg.navVoxelSize : 0.35f;
        float lambdaMin = MinLambda(cfg);
        return new SamplingContract(mesh, slope, route, nav, lambdaMin, SlopeClasses.ClassEpsilon,
            cfg.WalkMaxDegrees, cfg.WallMinDegrees);
    }

    /// <summary>
    /// Объявлен ли профиль, на котором поле v2 вообще определено (S1 §12, P0-6).
    /// Один профиль: Ø2000 и ≥48 квадов на чанк. На других разрешениях
    /// `Bands_NoOctaveBelowFourMeshSteps` не «зелёный» и не «красный» — он
    /// неприменим, и это надо назвать, а не подгонять порог.
    /// </summary>
    public static bool BandProfileSupported(float mapDiameter, int terrainQuads)
    {
        return Mathf.Abs(mapDiameter - BandProfileDiameter) <= 1f
            && terrainQuads >= BandProfileMinQuads;
    }

    /// <summary>Самая короткая волна октавы: v1 — от baseFrequency, v2 — от микрополосы.</summary>
    static float MinLambda(DomeGenConfigSO cfg)
    {
        if (cfg.WorldVersion >= 2)
            return DomeHeightField.MinBandLambda(DomeHeightField.DomeHeightParams.From(cfg));
        float baseLambda = 1f / Mathf.Max(0.0001f, cfg.baseFrequency);
        return baseLambda / Mathf.Pow(2f, Mathf.Max(0, cfg.octaves - 1));
    }

    public override string ToString() =>
        $"mesh={meshStep:F2} slopeEps={slopeEpsilon:F2} route={routeCell:F2} nav={navVoxel:F2} " +
        $"lambdaMin={lambdaMin:F2} classEps={classEpsilon:F2} " +
        $"class=[≤{walkMaxDegrees:F0};>{wallMinDegrees:F0}]";
}
