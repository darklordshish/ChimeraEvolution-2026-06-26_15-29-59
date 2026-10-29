using UnityEngine;

/// <summary>
/// Разрешения, на которых один и тот же пол читают разные потребители.
/// Гарантия обязана называть разрешение: «связно» без клетки — не гарантия.
/// </summary>
public readonly struct SamplingContract
{
    public readonly float meshStep;
    public readonly float slopeEpsilon;
    public readonly float routeCell;
    public readonly float navVoxel;

    public SamplingContract(float meshStep, float slopeEpsilon, float routeCell, float navVoxel)
    {
        this.meshStep = meshStep;
        this.slopeEpsilon = slopeEpsilon;
        this.routeCell = routeCell;
        this.navVoxel = navVoxel;
    }

    public static SamplingContract From(DomeGenConfigSO cfg, int terrainQuads, int routeRes = 128)
    {
        if (cfg == null) return default;
        float mesh = cfg.chunkSize / Mathf.Max(4, terrainQuads);
        float slope = Mathf.Max(0.5f, mesh * 0.5f);
        float route = cfg.mapDiameter / Mathf.Max(32, routeRes);
        float nav = cfg.navVoxelSize > 0f ? cfg.navVoxelSize : 0.35f;
        return new SamplingContract(mesh, slope, route, nav);
    }

    public override string ToString() =>
        $"mesh={meshStep:F2} slopeEps={slopeEpsilon:F2} route={routeCell:F2} nav={navVoxel:F2}";
}
