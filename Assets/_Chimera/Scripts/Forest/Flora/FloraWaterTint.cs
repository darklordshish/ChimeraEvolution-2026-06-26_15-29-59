using UnityEngine;

/// <summary>
/// Мокрое дно: затемнение клеток террейна ниже waterLevel. Чистая функция
/// (применяется хуком в PaintRelief, s5-правка тем же заходом). Лес, слайс s8.
/// </summary>
public static class FloraWaterTint
{
    static readonly Color Deep = new Color(0.16f, 0.22f, 0.25f);

    public static Color[] TintCells(Color[] cells, WorldGenConfigSO cfg, int res, float step)
    {
        if (cells == null || cfg == null) return cells;
        var out_ = new Color[cells.Length];
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float h = WorldHeightField.SampleHeight(cfg, ix * step, iz * step);
                Color c = cells[iz * res + ix];
                if (h < cfg.waterLevel)
                {
                    float depth = Mathf.Clamp01((cfg.waterLevel - h) / Mathf.Max(cfg.amplitude, 1e-6f));
                    c = Color.Lerp(c, Deep, 0.35f + 0.45f * depth);
                }
                out_[iz * res + ix] = c;
            }
        return out_;
    }
}
